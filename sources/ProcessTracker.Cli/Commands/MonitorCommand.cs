using ProcessTracker.Cli.Logging;
using ProcessTracker.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Command to display an interactive monitoring console with live updates
/// </summary>
public class MonitorCommand : Command<MonitorSettings>
{
   public override int Execute(CommandContext context, MonitorSettings settings)
   {
      var logger = new CliLogger();

      try
      {
         using var service = new ProcessMonitorService(logger);

         if (service.IsAlreadyRunning)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[yellow]Warning:[/] Another instance of ProcessTracker is already running.");
            return 1;
         }

         if (!settings.QuietMode)
            AnsiConsole.Clear();

         if (!settings.QuietMode)
         {
            AnsiConsole.Write(new FigletText("Process Monitor")
                .Color(Color.Blue)
                .Centered());
            AnsiConsole.MarkupLine("[green]Interactive Process Monitor Started[/]");
            AnsiConsole.MarkupLine("Press [yellow]Ctrl+C[/] to exit. Monitoring will continue in the background.");
         }

         var keepRunning = true;
         Console.CancelKeyPress += (sender, e) =>
         {
            e.Cancel = true;
            keepRunning = false;
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[blue]Exiting monitor. Process tracking continues in the background.[/]");
         };

         while (keepRunning)
         {
            if (!settings.QuietMode)
               AnsiConsole.Clear();

            var processPairs = service.GetAllProcessPairs();

            if (!settings.QuietMode)
            {
               AnsiConsole.Write(new FigletText("Process Monitor")
                   .Color(Color.Blue)
                   .Centered());
               AnsiConsole.MarkupLine($"[grey]Last updated: {DateTime.Now.ToLongTimeString()}[/]");
            }

            if (processPairs.Count == 0)
            {
               if (!settings.QuietMode)
               {
                  AnsiConsole.MarkupLine("\n[blue]No process pairs are currently being tracked.[/]");
                  AnsiConsole.MarkupLine("Add process pairs with: [green]proctrack add --main <id> --child <id>[/]");

                  if (AnsiConsole.Confirm("No processes being monitored. Exit monitor?", false))
                     break;
               }
               else
               {
                  Thread.Sleep(settings.RefreshInterval * 1000);
                  continue;
               }
            }
            else if (!settings.QuietMode)
            {
               var table = new Table();

               table.AddColumn(new TableColumn("Main Process").Centered());
               table.AddColumn(new TableColumn("Main ID").Centered());
               table.AddColumn(new TableColumn("Status").Centered());
               table.AddColumn(new TableColumn("Child Process").Centered());
               table.AddColumn(new TableColumn("Child ID").Centered());
               table.AddColumn(new TableColumn("Action").Centered());

               foreach (var pair in processPairs)
               {
                  var mainRunning = IsProcessRunning(pair.MainProcessId);
                  var childRunning = IsProcessRunning(pair.ChildProcessId);

                  var mainStatus = mainRunning ? "[green]Running[/]" : "[red]Stopped[/]";
                  var childStatus = childRunning ? "[green]Running[/]" : "[red]Stopped[/]";
                  var action = "";

                  if (!mainRunning && childRunning)
                     action = "[yellow]Will terminate[/]";

                  table.AddRow(
                      pair.MainProcessName,
                      pair.MainProcessId.ToString(),
                      mainStatus,
                      pair.ChildProcessName,
                      pair.ChildProcessId.ToString(),
                      action
                  );
               }

               AnsiConsole.Write(table);

               AnsiConsole.MarkupLine("\n[blue]Commands:[/]");
               AnsiConsole.MarkupLine("  [green]add[/] - Add a new process pair");
               AnsiConsole.MarkupLine("  [red]remove[/] - Remove a process pair");
               AnsiConsole.MarkupLine("  [yellow]list[/] - List all process pairs");
               AnsiConsole.MarkupLine("  [blue]Ctrl+C[/] - Exit monitor mode");
            }

            Thread.Sleep(settings.RefreshInterval * 1000);
         }

         return 0;
      }
      catch (Exception ex)
      {
         if (!settings.QuietMode)
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
         return 1;
      }
   }

   private bool IsProcessRunning(int processId)
   {
      try
      {
         Process.GetProcessById(processId);
         return true;
      }
      catch
      {
         return false;
      }
   }
}
