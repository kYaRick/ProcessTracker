using ProcessTracker.Cli.Logging;
using ProcessTracker.Cli.Services;
using ProcessTracker.Models;
using ProcessTracker.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Command to display an interactive monitoring console with live updates
/// </summary>
public class MonitorCommand : Command<MonitorSettings>
{
   private bool _keepRunning = true;
   private ProcessMonitorService? _service;
   private readonly ConcurrentQueue<string> _logMessages = new();
   private readonly int _maxLogMessages = 10;

   public override int Execute(CommandContext context, MonitorSettings settings)
   {
      try
      {
         IProcessTrackerLogger logger = settings.QuietMode
            ? new QuiteLogger()
            : new MonitorLogger(_logMessages, _maxLogMessages);

         var (service, _) = ServiceManager.GetOrCreateService(settings.QuietMode, logger);
         _service = service;

         if (service.IsAlreadyRunning)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[yellow]Warning:[/] Another instance of ProcessTracker is already running.");
            return 1;
         }

         _keepRunning = true;
         Console.CancelKeyPress += (_, e) =>
         {
            e.Cancel = true;
            _keepRunning = false;
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[blue]Exiting monitor. Process tracking continues in the background.[/]");
         };

         var emptyRefreshCount = 0;
         var maxEmptyRefreshes = settings.AutoExitTimeout > 0
            ? settings.AutoExitTimeout / settings.RefreshInterval
            : 0;

         if (!settings.QuietMode)
         {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[blue]Process Tracker Monitor[/]").RuleStyle("blue"));
            AnsiConsole.WriteLine();
         }

         while (_keepRunning)
         {
            if (settings.QuietMode)
            {
               Task.Delay(settings.RefreshInterval * 1000)
                  .GetAwaiter()
                  .GetResult();

               continue;
            }

            var processPairs = service.GetAllProcessPairs();

            var layout = new Layout("Root")
               .SplitRows(
                  new Layout("Top"),
                  new Layout("Bottom")
               );

            if (processPairs.Count == 0)
            {
               var panel = new Panel(
                  new Markup("[yellow]No processes are being tracked[/]")
               )
               .Border(BoxBorder.Rounded)
               .Header("[blue]Monitored Processes[/]")
               .Expand();

               layout["Top"].Update(panel);

               if (settings.AutoExitTimeout > 0)
               {
                  emptyRefreshCount++;
                  var remainingTime = (maxEmptyRefreshes - emptyRefreshCount) * settings.RefreshInterval;

                  if (emptyRefreshCount >= maxEmptyRefreshes)
                  {
                     AnsiConsole.Clear();
                     AnsiConsole.MarkupLine("[yellow]Auto-exit timeout reached. Exiting monitor.[/]");
                     _keepRunning = false;
                     break;
                  }

                  _logMessages.Enqueue($"[yellow]Auto-exit in {remainingTime} seconds[/]");
               }
            }
            else
            {
               emptyRefreshCount = 0;

               var table = CreateProcessTable(processPairs);
               layout["Top"].Update(table);
            }

            var logPanel = CreateLogPanel();
            layout["Bottom"].Update(logPanel);

            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[blue]Process Tracker Monitor[/]").RuleStyle("blue"));
            AnsiConsole.Write(
               new Markup($"[grey]Last updated: {DateTime.Now.ToLongTimeString()} • Press Ctrl+C to exit[/]")
               .Centered());
            AnsiConsole.WriteLine();
            AnsiConsole.Write(layout);

            Task.Delay(settings.RefreshInterval * 1000).Wait();
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

   private Table CreateProcessTable(IReadOnlyList<ProcessPair> processPairs)
   {
      var table = new Table()
         .Border(TableBorder.Rounded)
         .Title("[blue]Monitored Processes[/]")
         .Expand();

      table.AddColumn(new TableColumn("Main Process").Width(18));
      table.AddColumn(new TableColumn("Main ID").Width(8).Centered());
      table.AddColumn(new TableColumn("Status").Width(8).Centered());
      table.AddColumn(new TableColumn("Child Process").Width(18));
      table.AddColumn(new TableColumn("Child ID").Width(8).Centered());
      table.AddColumn(new TableColumn("Added").Width(16).Centered());

      foreach (var pair in processPairs)
      {
         var mainRunning = IsProcessRunning(pair.MainProcessId);
         var childRunning = IsProcessRunning(pair.ChildProcessId);

         var status = string.Empty;

         if (mainRunning && childRunning)
            status = "[green]●[/]";
         else if (mainRunning)
            status = "[blue]●[/]";
         else if (childRunning)
            status = "[yellow]●[/]";
         else
            status = "[red]●[/]";

         var mainName = TruncateName(pair.MainProcessName, 15);
         var childName = TruncateName(pair.ChildProcessName, 15);
         var time = pair.Time.ToString("yyyy-MM-dd HH:mm");

         table.AddRow(mainName, pair.MainProcessId.ToString(), status,
                     childName, pair.ChildProcessId.ToString(), time);
      }

      return table;
   }

   private Panel CreateLogPanel()
   {
      var logContent = string.Join("\n", _logMessages.Reverse().Take(_maxLogMessages));

      return new Panel(new Markup(logContent))
         .Header("[blue]Event Log[/]")
         .Border(BoxBorder.Rounded)
         .Expand();
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
