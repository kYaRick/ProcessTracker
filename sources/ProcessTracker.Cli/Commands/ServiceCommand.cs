using ProcessTracker.Cli.Logging;
using ProcessTracker.Processes;
using ProcessTracker.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Command to run the process tracker as a persistent service
/// </summary>
public class ServiceCommand : Command<BasicCommandSettings>
{
   public override int Execute(CommandContext context, BasicCommandSettings settings)
   {
      var logger = new CliLogger();

      try
      {
         if (!settings.QuietMode)
         {
            AnsiConsole.Write(
                new FigletText("Process Tracker")
                    .Color(Color.Blue)
                    .Centered());
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]Starting Process Tracker Service...[/]");
         }

         // Create service components with detailed logging
         var monitor = new ProcessMonitor(TimeSpan.FromSeconds(2), logger);
         var repository = new ProcessRepository();
         var singleInstance = new SingleInstanceManager(logger);

         using var service = new ProcessMonitorService(monitor, repository, singleInstance, logger);

         if (service.IsAlreadyRunning)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[yellow]Warning:[/] Another instance of ProcessTracker is already running.");
            return 1;
         }

         if (!settings.QuietMode)
         {
            AnsiConsole.MarkupLine("[green]Process Tracker Service is running[/]");
            AnsiConsole.MarkupLine("Press Ctrl+C to stop the service");

            var pairs = service.GetAllProcessPairs();
            if (pairs.Count > 0)
            {
               AnsiConsole.MarkupLine($"\n[blue]Currently tracking {pairs.Count} process pairs:[/]");
               var table = new Table();
               table.AddColumn("Main Process");
               table.AddColumn("Main ID");
               table.AddColumn("Child Process");
               table.AddColumn("Child ID");

               foreach (var pair in pairs)
               {
                  table.AddRow(
                      pair.MainProcessName,
                      pair.MainProcessId.ToString(),
                      pair.ChildProcessName,
                      pair.ChildProcessId.ToString()
                  );
               }

               AnsiConsole.Write(table);
            }
            else
            {
               AnsiConsole.MarkupLine("\n[blue]No processes are currently being tracked[/]");
               AnsiConsole.MarkupLine("Use [green]proctrack add[/] command to add process pairs");
            }
         }

         var exitEvent = new ManualResetEvent(false);

         Console.CancelKeyPress += (sender, e) =>
         {
            e.Cancel = true;
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[blue]Shutting down Process Tracker Service...[/]");
            exitEvent.Set();
         };

         exitEvent.WaitOne();

         if (!settings.QuietMode)
            AnsiConsole.MarkupLine("[green]Process Tracker Service stopped[/]");

         return 0;
      }
      catch (Exception ex)
      {
         if (!settings.QuietMode)
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
         return 1;
      }
   }
}

