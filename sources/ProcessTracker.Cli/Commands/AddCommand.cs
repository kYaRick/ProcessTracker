using ProcessTracker.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Command to add a process pair to tracking
/// </summary>
public class AddCommand : Command<ProcessPairSettings>
{
   public override int Execute(CommandContext context, ProcessPairSettings settings)
   {
      try
      {
         var success = ServiceManager.WithTemporarilySuspendedService
            (
               service =>
                  service.AddProcessPair(settings.MainProcessId, settings.ChildProcessId),
               settings.QuietMode
            );

         if (!success)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[red]Error:[/] Failed to add process pair");
            return 1;
         }

         if (!settings.QuietMode)
         {
            var mainProcessName = GetProcessName(settings.MainProcessId);
            var childProcessName = GetProcessName(settings.ChildProcessId);

            AnsiConsole.MarkupLine("[green]Success:[/] Process pair added to tracking");
            AnsiConsole.MarkupLine($"  Main process: {mainProcessName} (ID: {settings.MainProcessId})");
            AnsiConsole.MarkupLine($"  Child process: {childProcessName} (ID: {settings.ChildProcessId})");
            AnsiConsole.MarkupLine("[blue]ProcessTracker is monitoring these processes in the background.[/]");
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

   private string GetProcessName(int processId)
   {
      var processName = string.Empty;

      try
      {
         processName = Process.GetProcessById(processId).ProcessName;
      }
      catch { }

      return processName;
   }
}

