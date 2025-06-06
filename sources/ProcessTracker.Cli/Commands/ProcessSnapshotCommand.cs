using ConfigRunner;
using ConfigRunner.Constants;
using ConfigRunner.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;
using static ProcessTracker.Cli.Commands.MonitorSettings;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Command to manage processes before and after operations
/// </summary>
public class ProcessSnapshotCommand : Command<ProcessSnapshotCommandSettings>
{
   private readonly IConfigurationManager _configManager;

   private const string SNAPSHOT_FILENAME = "process_snapshot";

   public ProcessSnapshotCommand()
   {
      _configManager = new ConfigurationManager(ConfigurationType.Temp, "ProcessTracker");
   }

   public override int Execute(CommandContext context, ProcessSnapshotCommandSettings settings)
   {
      try
      {
         if (string.IsNullOrWhiteSpace(settings.ProcessName))
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[yellow]Please specify a process name using the --process option[/]");
            return 1;
         }

         if (settings.Before)
         {
            return TakeBeforeSnapshot(settings);
         }
         else if (settings.After)
         {
            return TakeAfterSnapshotAndCloseNewProcesses(settings);
         }
         else
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[yellow]Please specify either --before or --after option[/]");
            return 1;
         }
      }
      catch (Exception ex)
      {
         if (!settings.QuietMode)
            AnsiConsole.MarkupLine($"[red]Error processing command: {ex.Message}[/]");
         return 1;
      }
   }

   private int TakeBeforeSnapshot(ProcessSnapshotCommandSettings settings)
   {
      var processes = Process.GetProcessesByName(settings.ProcessName);
      var processIds = processes.Select(p => p.Id).ToList();

      var snapshotConfig = new ProcessSnapshotConfig
      {
         ProcessName = settings.ProcessName,
         ProcessIds = processIds
      };

      var saved = _configManager.SaveConfiguration(snapshotConfig, GetSnapshotFileName(settings.ProcessName));

      if (!settings.QuietMode)
      {
         if (saved)
         {
            AnsiConsole.MarkupLine($"[green]Snapshot taken: {processIds.Count} '{settings.ProcessName}' processes found[/]");
            foreach (var processId in processIds)
            {
               AnsiConsole.MarkupLine($"  Process ID: {processId}");
            }
            AnsiConsole.MarkupLine($"[blue]Snapshot saved to configuration[/]");
         }
         else
         {
            AnsiConsole.MarkupLine($"[red]Failed to save snapshot configuration[/]");
         }
      }

      return saved ? 0 : 1;
   }

   private int TakeAfterSnapshotAndCloseNewProcesses(ProcessSnapshotCommandSettings settings)
   {
      var fileName = GetSnapshotFileName(settings.ProcessName);

      if (!_configManager.ConfigurationExists(fileName))
      {
         if (!settings.QuietMode)
            AnsiConsole.MarkupLine($"[yellow]No previous snapshot found for '{settings.ProcessName}'. Please run with --before first.[/]");
         return 1;
      }

      var snapshotConfig = _configManager.ReadConfiguration<ProcessSnapshotConfig>(fileName);

      if (snapshotConfig is null || snapshotConfig.ProcessIds is null || snapshotConfig.ProcessName != settings.ProcessName)
      {
         if (!settings.QuietMode)
            AnsiConsole.MarkupLine($"[yellow]Invalid or corrupted snapshot data for '{settings.ProcessName}'[/]");

         _configManager.RemoveConfigurationFile(fileName);
         return 1;
      }

      var beforeProcessIds = new HashSet<int>(snapshotConfig.ProcessIds);
      var currentProcesses = Process.GetProcessesByName(settings.ProcessName);
      var newProcesses = currentProcesses.Where(p => !beforeProcessIds.Contains(p.Id)).ToList();

      if (!settings.QuietMode)
      {
         AnsiConsole.MarkupLine($"[blue]Found {newProcesses.Count} new '{settings.ProcessName}' processes since the last snapshot[/]");

         if (newProcesses.Count > 0)
         {
            AnsiConsole.MarkupLine($"[green]Closing new '{settings.ProcessName}' processes:[/]");
            foreach (var process in newProcesses)
            {
               AnsiConsole.MarkupLine($"  Closing process ID: {process.Id}");
            }
         }
      }

      var allSuccessful = true;
      foreach (var process in newProcesses)
      {
         try
         {
            process.CloseMainWindow();

            if (!process.WaitForExit(3000))
            {
               process.Kill();
            }
         }
         catch (Exception ex)
         {
            allSuccessful = false;
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine($"[red]Error closing process {process.Id}: {ex.Message}[/]");
         }
      }

      var deleted = _configManager.RemoveConfigurationFile(fileName);

      if (!settings.QuietMode && deleted)
      {
         AnsiConsole.MarkupLine($"[blue]Snapshot configuration cleaned up[/]");
      }

      return allSuccessful ? 0 : 1;
   }

   private string GetSnapshotFileName(string processName) =>
      $"{SNAPSHOT_FILENAME}_{processName.ToLowerInvariant()}";
}

/// <summary>
/// Configuration class for storing process snapshot data
/// </summary>
public class ProcessSnapshotConfig
{
   /// <summary>
   /// Name of the monitored process
   /// </summary>
   public string ProcessName { get; set; } = string.Empty;

   /// <summary>
   /// List of process IDs in the snapshot
   /// </summary>
   public List<int> ProcessIds { get; set; } = new();
}