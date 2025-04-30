using ProcessTracker.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Command to stop the background monitor process
/// </summary>
public class StopCommand : Command<BasicCommandSettings>
{
   public override int Execute(CommandContext context, BasicCommandSettings settings)
   {
      try
      {
         bool wasRunning = BackgroundLauncher.IsBackgroundMonitorRunning();

         if (!wasRunning)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[blue]Info:[/] No background monitor is currently running.");
            return 0;
         }

         bool success = BackgroundLauncher.TerminateBackgroundMonitor();

         if (success)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[green]Success:[/] Background monitor has been stopped.");
            return 0;
         }
         else
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[red]Error:[/] Failed to stop background monitor.");
            return 1;
         }
      }
      catch (Exception ex)
      {
         if (!settings.QuietMode)
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
         return 1;
      }
   }
}
