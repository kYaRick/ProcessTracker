using ProcessTracker.Cli.Logging;
using ProcessTracker.Cli.Services;
using ProcessTracker.Models;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Command to add a new process pair to monitor
/// </summary>
public class AddCommand : Command<ProcessPairSettings>
{
   public override int Execute(CommandContext context, ProcessPairSettings settings)
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

         if (wasCreated && !settings.QuietMode)
            AnsiConsole.MarkupLine("[blue]Process Tracker service started[/]");

         var mainProcessName = GetProcessName(settings.MainProcessId);
         var childProcessName = GetProcessName(settings.ChildProcessId);

         if (string.IsNullOrEmpty(mainProcessName))
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine($"[red]Error:[/] Process with ID {settings.MainProcessId} not found.");
            return 1;
         }

         if (string.IsNullOrEmpty(childProcessName))
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine($"[red]Error:[/] Process with ID {settings.ChildProcessId} not found.");
            return 1;
         }

         var added = service.AddProcessPair(settings.MainProcessId, settings.ChildProcessId);

         if (!added)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[red]Error:[/] Failed to add process pair.");
            return 1;
         }

         if (!settings.QuietMode)
         {
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
      try
      {
         var process = Process.GetProcessById(processId);
         return process.ProcessName;
      }
      catch
      {
         return string.Empty;
      }
   }
}

