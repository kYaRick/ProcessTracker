using ProcessTracker.Cli.Logging;
using ProcessTracker.Cli.Services;
using ProcessTracker.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Command to stop the process tracker service
/// </summary>
public class StopCommand : Command<StopSettings>
{
   public override int Execute(CommandContext context, StopSettings settings)
   {
      IProcessTrackerLogger logger = settings.QuietMode ? new QuiteLogger() : new CliLogger();

      try
      {
         var (service, wasCreated) = ServiceManager.GetOrCreateService(settings.QuietMode);

         if (service.IsAlreadyRunning)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[yellow]Warning:[/] Another instance of ProcessTracker is already running.");
            return 1;
         }

         var processPairs = service.GetAllProcessPairs();

         if (processPairs.Count == 0 || settings.Force)
         {
            if (!settings.QuietMode)
            {
               if (processPairs.Count == 0)
                  AnsiConsole.MarkupLine("[blue]Info:[/] No processes are being monitored. Stopping the tracker.");
               else
                  AnsiConsole.MarkupLine($"[yellow]Warning:[/] Force stopping tracker with {processPairs.Count} process pairs still being monitored.");
            }

            ServiceManager.ShutdownService();

            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[green]Success:[/] Process tracker has been stopped.");

            return 0;
         }

         if (!settings.QuietMode)
         {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Cannot stop: {processPairs.Count} process pairs are still being monitored.");
            AnsiConsole.MarkupLine("Use 'stop --force' to force stop or remove all process pairs first.");

            if (AnsiConsole.Confirm("Stop tracker anyway?", false))
            {
               ServiceManager.ShutdownService();
               AnsiConsole.MarkupLine("[green]Success:[/] Process tracker has been stopped.");
               return 0;
            }
         }

         return 1;
      }
      catch (Exception ex)
      {
         if (!settings.QuietMode)
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
         return 1;
      }
   }
}

