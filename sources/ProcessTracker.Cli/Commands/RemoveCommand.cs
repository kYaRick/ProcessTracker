using ProcessTracker.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Command to remove a process pair from tracking
/// </summary>
public class RemoveCommand : Command<ProcessPairSettings>
{
   public override int Execute(CommandContext context, ProcessPairSettings settings)
   {
      try
      {
         var success = ServiceManager.WithTemporarilySuspendedService(service =>
         {
            return service.RemoveProcessPair(settings.MainProcessId, settings.ChildProcessId);
         }, settings.QuietMode);

         if (!success)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[red]Error:[/] Process pair not found or could not be removed");
            return 1;
         }

         if (!settings.QuietMode)
            AnsiConsole.MarkupLine("[green]Success:[/] Process pair removed from tracking");

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

