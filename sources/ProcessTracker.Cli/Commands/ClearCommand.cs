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
      try
      {
         int processCount = ServiceManager.WithTemporarilySuspendedService(service =>
         {
            var allPairs = service.GetAllProcessPairs();
            int count = allPairs.Count;

            // Clear all process pairs from the repository
            var repository = new ProcessRepository();
            repository.SaveAll(new List<ProcessPair>());

            return count;
         }, settings.QuietMode);

         if (!settings.QuietMode)
         {
            if (processCount == 0)
               AnsiConsole.MarkupLine("[blue]Info:[/] No process pairs were being tracked.");
            else
               AnsiConsole.MarkupLine($"[green]Success:[/] Cleared {processCount} tracked process pairs.");
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
}

