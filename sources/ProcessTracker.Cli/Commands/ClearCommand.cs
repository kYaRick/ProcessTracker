using ProcessTracker.Cli.Logging;
using ProcessTracker.Cli.Services;
using ProcessTracker.Models;
using ProcessTracker.Processes;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Command to clear all tracked process pairs
/// </summary>
public class ClearCommand : Command<BasicCommandSettings>
{
   public override int Execute(CommandContext context, BasicCommandSettings settings)
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

         var processCount = service.GetAllProcessPairs().Count;

         if (processCount == 0)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[blue]Info:[/] No process pairs are currently being tracked.");
            return 0;
         }

         var repository = new ProcessRepository();
         repository.SaveAll(new List<ProcessPair>());

         if (!settings.QuietMode)
            AnsiConsole.MarkupLine($"[green]Success:[/] Cleared {processCount} tracked process pairs.");

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
