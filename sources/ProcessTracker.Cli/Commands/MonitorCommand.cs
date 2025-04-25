using ProcessTracker.Cli.Logging;
using ProcessTracker.Cli.Services;
using ProcessTracker.Models;
using ProcessTracker.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Command to display an interactive monitoring console with live updates
/// </summary>
public class MonitorCommand : Command<MonitorSettings>
{
   private readonly Dictionary<string, Action<MonitorSettings>> _commands = new(StringComparer.OrdinalIgnoreCase);
   private bool _keepRunning = true;
   private ProcessMonitorService? _service;
   private bool _verbose = false;

   public MonitorCommand()
   {
      // Register available commands during monitoring
      _commands["add"] = _ => AnsiConsole.MarkupLine("[yellow]To add a process pair, exit monitor mode and use:[/] proctrack add --main <id> --child <id>");
      _commands["remove"] = _ => AnsiConsole.MarkupLine("[yellow]To remove a process pair, exit monitor mode and use:[/] proctrack remove --main <id> --child <id>");
      _commands["list"] = _ => DisplayProcessList();
      _commands["exit"] = _ => _keepRunning = false;
      _commands["quit"] = _ => _keepRunning = false;
      _commands["verbose"] = s => ToggleVerboseMode(s);
      _commands["help"] = _ => DisplayHelp();
      _commands["?"] = _ => DisplayHelp();
      _commands["clear"] = _ => AnsiConsole.Clear();
   }

   public override int Execute(CommandContext context, MonitorSettings settings)
   {
      IProcessTrackerLogger logger = settings.QuietMode ? new QuiteLogger() : new CliLogger();
      _verbose = settings.Verbose;

      try
      {
         // Use ServiceManager to get a consistent service instance
         var (service, wasCreated) = ServiceManager.GetOrCreateService(settings.QuietMode);
         _service = service;

         if (service.IsAlreadyRunning)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[yellow]Warning:[/] Another instance of ProcessTracker is already running.");
            return 1;
         }

         if (!settings.QuietMode)
            AnsiConsole.Clear();

         if (!settings.QuietMode)
         {
            AnsiConsole.Write(new FigletText("Process Monitor")
                .Color(Color.Blue)
                .Centered());
            AnsiConsole.MarkupLine("[green]Interactive Process Monitor Started[/]");

            if (wasCreated)
               AnsiConsole.MarkupLine("[blue]Started new monitoring service[/]");
            else
               AnsiConsole.MarkupLine("[blue]Connected to existing monitoring service[/]");

            // Show auto-exit information if enabled
            if (settings.AutoExitTimeout > 0)
               AnsiConsole.MarkupLine($"[blue]Auto-exit:[/] Monitor will automatically exit after {settings.AutoExitTimeout} seconds with no processes");

            AnsiConsole.MarkupLine("\n[blue]Commands available during monitoring:[/]");
            AnsiConsole.MarkupLine("  Type [green]help[/] or [green]?[/] to show available commands");
            AnsiConsole.MarkupLine("  Type [green]exit[/] or press [green]Ctrl+C[/] to exit monitor mode");
            AnsiConsole.MarkupLine("  Press [green]Enter[/] to refresh the display");
            AnsiConsole.WriteLine();
         }

         _keepRunning = true;

         // Set up a cancellation token that will be triggered by Ctrl+C
         Console.CancelKeyPress += (sender, e) =>
         {
            e.Cancel = true;
            _keepRunning = false;
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[blue]Exiting monitor. Process tracking continues in the background.[/]");
         };

         // Auto-exit tracking variables
         int emptyRefreshCount = 0;
         int maxEmptyRefreshes = settings.AutoExitTimeout > 0
            ? settings.AutoExitTimeout / settings.RefreshInterval
            : 0;

         // Set up input handling in a separate task
         var inputTask = Task.Run(() => HandleUserInput(settings));

         while (_keepRunning)
         {
            if (!settings.QuietMode)
            {
               // Only clear screen if not in quiet mode
               AnsiConsole.Clear();

               AnsiConsole.Write(new FigletText("Process Monitor")
                  .Color(Color.Blue)
                  .Centered());
               AnsiConsole.MarkupLine($"[grey]Last updated: {DateTime.Now.ToLongTimeString()}[/]");
            }

            var processPairs = service.GetAllProcessPairs();

            if (processPairs.Count == 0)
            {
               // Track time with no processes for auto-exit
               if (settings.AutoExitTimeout > 0)
               {
                  emptyRefreshCount++;
                  int remainingTime = (maxEmptyRefreshes - emptyRefreshCount) * settings.RefreshInterval;

                  if (emptyRefreshCount >= maxEmptyRefreshes)
                  {
                     if (!settings.QuietMode)
                        AnsiConsole.MarkupLine("[yellow]Auto-exit timeout reached. Exiting monitor.[/]");
                     _keepRunning = false;
                     break;
                  }

                  if (!settings.QuietMode)
                  {
                     AnsiConsole.MarkupLine("\n[blue]No process pairs are currently being tracked.[/]");
                     AnsiConsole.MarkupLine($"[yellow]Auto-exit:[/] Monitor will exit in {remainingTime} seconds if no processes are added");
                  }
               }
               else if (!settings.QuietMode)
               {
                  AnsiConsole.MarkupLine("\n[blue]No process pairs are currently being tracked.[/]");
               }
            }
            else
            {
               // Reset empty refresh counter when processes are found
               emptyRefreshCount = 0;

               if (!settings.QuietMode)
               {
                  DisplayProcessTable(processPairs);
               }
            }

            if (!settings.QuietMode)
            {
               AnsiConsole.MarkupLine("\n[grey]Type a command or press Enter to refresh:[/] ");
            }

            // Wait for refresh interval
            Task.Delay(settings.RefreshInterval * 1000).GetAwaiter();
         }

         // Clean up input handling
         inputTask.Wait(TimeSpan.FromSeconds(1));

         return 0;
      }
      catch (Exception ex)
      {
         if (!settings.QuietMode)
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
         return 1;
      }
   }

   private void HandleUserInput(MonitorSettings settings)
   {
      while (_keepRunning)
      {
         // Read input without blocking the main thread
         if (Console.KeyAvailable)
         {
            var input = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(input))
            {
               ProcessCommand(input, settings);
            }
         }

         // Small delay to prevent CPU thrashing
         Thread.Sleep(100);
      }
   }

   private void ProcessCommand(string command, MonitorSettings settings)
   {
      if (_commands.TryGetValue(command, out var action))
      {
         action(settings);
      }
      else if (!string.IsNullOrWhiteSpace(command))
      {
         AnsiConsole.MarkupLine($"[yellow]Unknown command:[/] {command}");
         DisplayHelp();
      }
   }

   private void DisplayHelp()
   {
      AnsiConsole.MarkupLine("\n[blue]Available commands:[/]");
      AnsiConsole.MarkupLine("  [green]list[/] - Display current process pairs");
      AnsiConsole.MarkupLine("  [green]verbose[/] - Toggle verbose output mode");
      AnsiConsole.MarkupLine("  [green]clear[/] - Clear the console");
      AnsiConsole.MarkupLine("  [green]exit[/] or [green]quit[/] - Exit monitor mode");
      AnsiConsole.MarkupLine("  [green]help[/] or [green]?[/] - Show this help");
   }

   private void ToggleVerboseMode(MonitorSettings settings)
   {
      _verbose = !_verbose;
      settings.Verbose = _verbose;
      AnsiConsole.MarkupLine(_verbose
         ? "[green]Verbose mode enabled[/]"
         : "[yellow]Verbose mode disabled[/]");
   }

   private void DisplayProcessList()
   {
      if (_service == null) return;

      var processPairs = _service.GetAllProcessPairs();
      if (processPairs.Count == 0)
      {
         AnsiConsole.MarkupLine("[blue]No process pairs are currently being tracked.[/]");
         return;
      }

      DisplayProcessTable(processPairs);
   }

   private void DisplayProcessTable(IReadOnlyList<ProcessPair> processPairs)
   {
      var table = new Table();

      table.AddColumn(new TableColumn("Main Process").Centered());
      table.AddColumn(new TableColumn("Main ID").Centered());
      table.AddColumn(new TableColumn("Status").Centered());
      table.AddColumn(new TableColumn("Child Process").Centered());
      table.AddColumn(new TableColumn("Child ID").Centered());
      table.AddColumn(new TableColumn("Action").Centered());

      if (_verbose)
      {
         table.AddColumn(new TableColumn("Added").Centered());
      }

      foreach (var pair in processPairs)
      {
         var mainRunning = IsProcessRunning(pair.MainProcessId);
         var childRunning = IsProcessRunning(pair.ChildProcessId);

         var mainStatus = mainRunning ? "[green]Running[/]" : "[red]Stopped[/]";
         var childStatus = childRunning ? "[green]Running[/]" : "[red]Stopped[/]";
         var action = "";

         if (!mainRunning && childRunning)
            action = "[yellow]Will terminate[/]";

         if (_verbose)
         {
            table.AddRow(
                pair.MainProcessName,
                pair.MainProcessId.ToString(),
                mainStatus,
                pair.ChildProcessName,
                pair.ChildProcessId.ToString(),
                action,
                pair.Time.ToString("yyyy-MM-dd HH:mm:ss")
            );
         }
         else
         {
            table.AddRow(
                pair.MainProcessName,
                pair.MainProcessId.ToString(),
                mainStatus,
                pair.ChildProcessName,
                pair.ChildProcessId.ToString(),
                action
            );
         }
      }

      AnsiConsole.Write(table);

      // Only show termination notifications in verbose mode
      if (_verbose)
      {
         foreach (var pair in processPairs)
         {
            if (!IsProcessRunning(pair.MainProcessId) && IsProcessRunning(pair.ChildProcessId))
            {
               AnsiConsole.MarkupLine($"[yellow]Main process {pair.MainProcessName} ({pair.MainProcessId}) has terminated. Child process {pair.ChildProcessName} ({pair.ChildProcessId}) will be terminated.[/]");
            }
         }
      }
   }

   private bool IsProcessRunning(int processId)
   {
      try
      {
         var process = Process.GetProcessById(processId);
         return !process.HasExited;
      }
      catch
      {
         return false;
      }
   }
}
