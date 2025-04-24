using ProcessTracker.Cli.Logging;
using ProcessTracker.Cli.Services;
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
         // Use ServiceManager to get a consistent service instance
         var (service, wasCreated) = ServiceManager.GetOrCreateService(settings.QuietMode);

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

            if (wasCreated)
               AnsiConsole.MarkupLine("[blue]Started new monitoring service[/]");
            else
               AnsiConsole.MarkupLine("[blue]Connected to existing monitoring service[/]");

            AnsiConsole.MarkupLine("Press [yellow]Ctrl+C[/] to exit. Monitoring will continue in the background.");

            // Show auto-exit information if enabled
            if (settings.AutoExitTimeout > 0)
               AnsiConsole.MarkupLine($"[blue]Auto-exit:[/] Monitor will automatically exit after {settings.AutoExitTimeout} seconds with no processes");
         }

         var keepRunning = true;
         Console.CancelKeyPress += (sender, e) =>
         {
            e.Cancel = true;
            keepRunning = false;
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[blue]Exiting monitor. Process tracking continues in the background.[/]");
         };

         // Auto-exit tracking variables
         int emptyRefreshCount = 0;
         int maxEmptyRefreshes = settings.AutoExitTimeout > 0
            ? settings.AutoExitTimeout / settings.RefreshInterval
            : 0;

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
               // Track time with no processes for auto-exit
               if (settings.AutoExitTimeout > 0)
               {
                  emptyRefreshCount++;
                  int remainingTime = (maxEmptyRefreshes - emptyRefreshCount) * settings.RefreshInterval;

                  if (emptyRefreshCount >= maxEmptyRefreshes)
                  {
                     if (!settings.QuietMode)
                        AnsiConsole.MarkupLine("[yellow]Auto-exit timeout reached. Exiting monitor.[/]");
                     break;
                  }

                  if (!settings.QuietMode)
                  {
                     AnsiConsole.MarkupLine("\n[blue]No process pairs are currently being tracked.[/]");
                     AnsiConsole.MarkupLine("Add process pairs with: [green]proctrack add --main <id> --child <id>[/]");
                     AnsiConsole.MarkupLine($"[yellow]Auto-exit:[/] Monitor will exit in {remainingTime} seconds if no processes are added");

                     if (AnsiConsole.Confirm("No processes being monitored. Exit monitor now?", false))
                        break;
                  }
               }
               else if (!settings.QuietMode)
               {
                  AnsiConsole.MarkupLine("\n[blue]No process pairs are currently being tracked.[/]");
                  AnsiConsole.MarkupLine("Add process pairs with: [green]proctrack add --main <id> --child <id>[/]");

                  if (AnsiConsole.Confirm("No processes being monitored. Exit monitor?", false))
                     break;
               }
               else
               {
                  // In quiet mode, handle auto-exit without prompts
                  if (settings.AutoExitTimeout > 0)
                  {
                     emptyRefreshCount++;
                     if (emptyRefreshCount >= maxEmptyRefreshes)
                        break;
                  }

                  Thread.Sleep(settings.RefreshInterval * 1000);
                  continue;
               }
            }
            else
            {
               // Reset empty refresh counter when processes are found
               emptyRefreshCount = 0;

               if (!settings.QuietMode)
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

                  // Check if any processes need to be terminated
                  // This is to make the monitor more responsive
                  foreach (var pair in processPairs)
                  {
                     if (!IsProcessRunning(pair.MainProcessId) && IsProcessRunning(pair.ChildProcessId))
                     {
                        if (!settings.QuietMode)
                           AnsiConsole.MarkupLine($"\n[yellow]Main process {pair.MainProcessName} ({pair.MainProcessId}) has terminated. Child process {pair.ChildProcessName} ({pair.ChildProcessId}) will be terminated.[/]");
                     }
                  }

                  AnsiConsole.MarkupLine("\n[blue]Commands:[/]");
                  AnsiConsole.MarkupLine("  [green]add[/] - Add a new process pair");
                  AnsiConsole.MarkupLine("  [red]remove[/] - Remove a process pair");
                  AnsiConsole.MarkupLine("  [yellow]list[/] - List all process pairs");
                  AnsiConsole.MarkupLine("  [blue]Ctrl+C[/] - Exit monitor mode");
               }
            }

            Thread.Sleep(settings.RefreshInterval * 1000);
         }

         // Important: Do NOT dispose the service when exiting the monitor
         // This would stop the tracking functionality
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
         var process = Process.GetProcessById(processId);
         return !process.HasExited;
      }
      catch
      {
         return false;
      }
   }
}

