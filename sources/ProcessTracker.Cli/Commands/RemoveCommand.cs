using ProcessTracker.Cli.Logging;
using ProcessTracker.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Command to remove a process pair from monitoring
/// </summary>
public class RemoveCommand : Command<ProcessPairSettings>
{
   public override int Execute(CommandContext context, ProcessPairSettings settings)
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

         var removed = service.RemoveProcessPair(settings.MainProcessId, settings.ChildProcessId);

         if (!removed)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[yellow]Warning:[/] Process pair not found or could not be removed.");
            return 1;
         }

         if (!settings.QuietMode)
         {
            AnsiConsole.MarkupLine("[green]Success:[/] Process pair removed from tracking");
            AnsiConsole.MarkupLine($"  Main process ID: {settings.MainProcessId}");
            AnsiConsole.MarkupLine($"  Child process ID: {settings.ChildProcessId}");
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
