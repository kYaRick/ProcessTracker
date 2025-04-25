using ProcessTracker.Cli.Logging;
using ProcessTracker.Models;
using ProcessTracker.Processes;
using ProcessTracker.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Command to run the process tracker as a persistent service
/// </summary>
public class ServiceCommand : Command<ServiceSettings>
{
   public override int Execute(CommandContext context, ServiceSettings settings)
   {
      IProcessTrackerLogger logger = settings.QuietMode ? new QuiteLogger() : new CliLogger();

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
         TimeSpan? autoShutdownTimeout = settings.AutoShutdownTimeout > 0
            ? TimeSpan.FromSeconds(settings.AutoShutdownTimeout)
            : null;

         var monitor = new ProcessMonitor(
            TimeSpan.FromSeconds(settings.CheckInterval),
            logger);

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

            if (settings.AutoShutdownTimeout > 0)
               AnsiConsole.MarkupLine($"[blue]Auto-shutdown:[/] Service will auto-shutdown after {settings.AutoShutdownTimeout} seconds with no processes");

            if (settings.AutoExit)
               AnsiConsole.MarkupLine("[blue]Auto-exit:[/] Service will terminate when auto-shutdown occurs");

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

         _exitEvent = new ManualResetEvent(false);

         Console.CancelKeyPress += (sender, e) =>
         {
            e.Cancel = true;
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[blue]Shutting down Process Tracker Service...[/]");
            _exitEvent.Set();
         };

         _exitEvent.WaitOne();

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

   private ManualResetEvent? _exitEvent;
}
