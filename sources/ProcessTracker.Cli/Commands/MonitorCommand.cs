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
   private readonly Dictionary<string, Action> _commands = new(StringComparer.OrdinalIgnoreCase);
   private bool _keepRunning = true;
   private ProcessMonitorService? _service;
   private int _lastProcessCount = -1;

   public MonitorCommand()
   {
      _commands["list"] = DisplayProcessList;
      _commands["exit"] = () => _keepRunning = false;
      _commands["quit"] = () => _keepRunning = false;
      _commands["help"] = DisplayHelp;
      _commands["?"] = DisplayHelp;
      _commands["clear"] = () => AnsiConsole.Clear();
   }

   public override int Execute(CommandContext context, MonitorSettings settings)
   {
      IProcessTrackerLogger logger = settings.QuietMode ? new QuiteLogger() : new CliLogger();

      try
      {
         var (service, _) = ServiceManager.GetOrCreateService(settings.QuietMode);
         _service = service;

         if (service.IsAlreadyRunning)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[yellow]Warning:[/] Another instance of ProcessTracker is already running.");
            return 1;
         }

         if (!settings.QuietMode)
         {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[blue]Process Monitor[/]").RuleStyle("blue").Centered());
            AnsiConsole.MarkupLine("[green]Process Monitor Started[/]");

            if (settings.AutoExitTimeout > 0)
               AnsiConsole.MarkupLine($"[blue]Auto-exit:[/] Monitor will exit after {settings.AutoExitTimeout} seconds with no processes");

            AnsiConsole.MarkupLine("[blue]Commands:[/] help, list, clear, exit");
            AnsiConsole.WriteLine();
         }

         _keepRunning = true;

         Console.CancelKeyPress += (_, e) =>
         {
            e.Cancel = true;
            _keepRunning = false;
         };

         var emptyRefreshCount = 0;
         var maxEmptyRefreshes = settings.AutoExitTimeout > 0
            ? settings.AutoExitTimeout / settings.RefreshInterval
            : 0;

         var inputTask = Task.Run(HandleUserInput);

         while (_keepRunning)
         {
            var processPairs = service.GetAllProcessPairs();
            bool processListChanged = processPairs.Count != _lastProcessCount;
            _lastProcessCount = processPairs.Count;

            if (processListChanged && !settings.QuietMode)
            {
               AnsiConsole.Clear();
               AnsiConsole.Write(new Rule("[blue]Process Monitor[/]").RuleStyle("blue"));
               AnsiConsole.MarkupLine($"[grey]Last updated: {DateTime.Now.ToLongTimeString()}[/]");
            }

            if (processPairs.Count == 0)
            {
               if (settings.AutoExitTimeout > 0)
               {
                  emptyRefreshCount++;
                  if (emptyRefreshCount >= maxEmptyRefreshes)
                  {
                     if (!settings.QuietMode)
                        AnsiConsole.MarkupLine("[yellow]Auto-exit timeout reached.[/]");
                     _keepRunning = false;
                     break;
                  }

                  if (!settings.QuietMode && (processListChanged || emptyRefreshCount % 2 == 0))
                  {
                     var remainingTime = (maxEmptyRefreshes - emptyRefreshCount) * settings.RefreshInterval;
                     AnsiConsole.MarkupLine("[blue]No processes tracked.[/] Auto-exit in [yellow]{0}[/] sec", remainingTime);
                  }
               }
               else if (!settings.QuietMode && processListChanged)
               {
                  AnsiConsole.MarkupLine("[blue]No processes are being tracked.[/]");
               }
            }
            else
            {
               emptyRefreshCount = 0;
               if (!settings.QuietMode && processListChanged)
                  DisplayProcessTable(processPairs);
            }

            if (!settings.QuietMode && processListChanged)
               AnsiConsole.MarkupLine("[dim]Commands: help, list, exit[/]");

            Task.Delay(settings.RefreshInterval * 1000).Wait();
         }

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

   private void HandleUserInput()
   {
      while (_keepRunning)
      {
         if (Console.KeyAvailable)
         {
            var input = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(input) && _commands.TryGetValue(input, out var action))
               action();
         }
         Thread.Sleep(100);
      }
   }

   private void DisplayHelp()
   {
      AnsiConsole.MarkupLine("[blue]Available commands:[/]");
      AnsiConsole.MarkupLine("  [green]list[/] - Display process pairs");
      AnsiConsole.MarkupLine("  [green]clear[/] - Clear the console");
      AnsiConsole.MarkupLine("  [green]exit[/] or [green]quit[/] - Exit monitor");
      AnsiConsole.MarkupLine("  [green]help[/] or [green]?[/] - Show this help");
   }

   private void DisplayProcessList()
   {
      if (_service == null) return;

      var processPairs = _service.GetAllProcessPairs();
      if (processPairs.Count == 0)
      {
         AnsiConsole.MarkupLine("[blue]No process pairs are being tracked.[/]");
         return;
      }

      DisplayProcessTable(processPairs);
   }

   private void DisplayProcessTable(IReadOnlyList<ProcessPair> processPairs)
   {
      var table = new Table().Border(TableBorder.Simple).BorderColor(Color.Grey);

      table.AddColumn(new TableColumn("Main").Width(18));
      table.AddColumn(new TableColumn("ID").Width(8).Centered());
      table.AddColumn(new TableColumn("Status").Width(8).Centered());
      table.AddColumn(new TableColumn("Child").Width(18));
      table.AddColumn(new TableColumn("ID").Width(8).Centered());

      foreach (var pair in processPairs)
      {
         var mainRunning = IsProcessRunning(pair.MainProcessId);
         var childRunning = IsProcessRunning(pair.ChildProcessId);

         var mainStatus = mainRunning ? "[green]●[/]" : "[red]●[/]";

         var mainName = TruncateName(pair.MainProcessName, 15);
         var childName = TruncateName(pair.ChildProcessName, 15);

         table.AddRow(
             mainName,
             pair.MainProcessId.ToString(),
             mainStatus,
             childName,
             pair.ChildProcessId.ToString()
         );
      }

      AnsiConsole.Write(table);
   }

   private string TruncateName(string name, int maxLength) =>
      name.Length <= maxLength ? name : name.Substring(0, maxLength - 3) + "...";

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
