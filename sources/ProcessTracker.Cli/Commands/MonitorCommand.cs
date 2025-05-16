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
   private readonly Stopwatch _uptime = new();
   private int _totalProcessed = 0;
   private Timer? _secondsTimer;
   private HashSet<(int MainId, int ChildId)> _lastKnownPairs = new();

   public override int Execute(CommandContext context, MonitorSettings settings)
   {
      try
      {
         if (settings.BackgroundMode)
         {
            var success = BackgroundLauncher.LaunchBackgroundMonitor(
               null,
               settings.RefreshInterval,
               settings.AutoExitTimeout);

            if (!settings.QuietMode)
            {
               if (success)
                  AnsiConsole.MarkupLine("[green]Background monitor started successfully.[/]");
               else
                  AnsiConsole.MarkupLine("[red]Failed to start background monitor.[/]");
            }

            return success ? 0 : 1;
         }

         IProcessTrackerLogger logger = settings.QuietMode
            ? new QuiteLogger()
            : new MonitorLogger(_logMessages, _maxLogMessages);

         var (service, _) = ServiceManager.GetOrCreateService(settings.QuietMode, logger);
         _service = service;

         if (service.IsAlreadyRunning)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[yellow]Warning:[/] Another instance of ProcessTracker is already running.");

            logger.Warning("Another instance is already running");
            return 1;
         }

         _keepRunning = true;
         _uptime.Start();

         if (!settings.QuietMode)
         {
            Console.CancelKeyPress += (_, e) =>
            {
               e.Cancel = true;
               _keepRunning = false;
               AnsiConsole.MarkupLine("[blue]Exiting monitor. Process tracking continues in the background.[/]");
            };
         }

         var emptyRefreshCount = 0;
         var maxEmptyRefreshes = settings.AutoExitTimeout > 0
            ? settings.AutoExitTimeout / settings.RefreshInterval
            : 0;

         if (!settings.QuietMode)
         {
            AnsiConsole.Clear();
            AnsiConsole.Write(new FigletText("ProcessTracker")
                .Centered()
                .Color(Color.Green));
            AnsiConsole.WriteLine();
         }

         var mainLayout = new Layout("Root")
            .SplitRows(
               new Layout("Header").Size(3),
               new Layout("Processes").Size(7),
               new Layout("Bottom")
                  .SplitColumns(
                     new Layout("Statistics").Size(30),
                     new Layout("Log")
                  ),
               new Layout("Footer").Size(1)
            );

         AnsiConsole.Live(mainLayout)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Start(ctx =>
            {
               _secondsTimer = new(_ =>
               {
                  try
                  {
                     if (_keepRunning && !settings.QuietMode)
                     {
                        mainLayout["Footer"].Update(
                           new Markup($"[grey]Press [blue]Ctrl+C[/] to exit • Refresh: {settings.RefreshInterval}s • Uptime: {FormatUptime()}[/]")
                        );
                        ctx.Refresh();
                     }
                  }
                  catch
                  {
                  }
               }, null, 0, 1000);

               try
               {
                  while (_keepRunning)
                  {
                     service.RefreshFromRepository();

                     var processPairs = service.GetAllProcessPairs();

                     var currentPairIds = new HashSet<(int MainId, int ChildId)>(
                         processPairs.Select(p => (p.MainProcessId, p.ChildProcessId)));

                     var addedPairs = currentPairIds.Except(_lastKnownPairs).ToList();
                     foreach (var pair in addedPairs)
                     {
                        _logMessages.Enqueue($"[green]Process pair added: {pair.MainId} → {pair.ChildId}[/]");
                     }

                     var removedPairs = _lastKnownPairs.Except(currentPairIds).ToList();
                     foreach (var pair in removedPairs)
                     {
                        _logMessages.Enqueue($"[yellow]Process pair removed: {pair.MainId} → {pair.ChildId}[/]");
                     }

                     _lastKnownPairs = currentPairIds;

                     if (settings.QuietMode)
                     {
                        if (processPairs.Count == 0 && settings.AutoExitTimeout > 0)
                        {
                           emptyRefreshCount++;

                           if (emptyRefreshCount >= maxEmptyRefreshes)
                           {
                              logger.Info("Auto-exit timeout reached with no processes to monitor. Exiting.");
                              _keepRunning = false;
                              break;
                           }
                        }
                        else
                        {
                           emptyRefreshCount = 0;
                        }

                        Task.Delay(settings.RefreshInterval * 1000).Wait();
                        continue;
                     }

                     mainLayout["Processes"].Size(processPairs.Count > 0 ? 15 : 7);

                     mainLayout["Header"].Update(
                        new Panel(CreateStatusBar(processPairs.Count, settings))
                           .NoBorder()
                           .Expand()
                     );

                     if (processPairs.Count == 0)
                     {
                        mainLayout["Processes"].Update(
                           new Panel(
                              new Markup("[yellow]No processes are being tracked[/]\n\n" +
                                        "Use [green]proctrack add[/] to add process pairs.\n" +
                                        "Press [blue]Ctrl+C[/] to exit monitoring.")
                           )
                           .Header("[blue]Monitored Processes[/]")
                           .Expand()
                        );

                        if (settings.AutoExitTimeout > 0)
                        {
                           emptyRefreshCount++;
                           var remainingTime = (maxEmptyRefreshes - emptyRefreshCount) * settings.RefreshInterval;

                           if (emptyRefreshCount >= maxEmptyRefreshes)
                           {
                              _keepRunning = false;
                              var markup = new Markup("[yellow]Auto-exit timeout reached. Exiting monitor.[/]");
                              ctx.UpdateTarget(markup);
                              break;
                           }

                           _logMessages.Enqueue($"[yellow]Auto-exit in {remainingTime} seconds[/]");
                        }
                     }
                     else
                     {
                        emptyRefreshCount = 0;
                        _totalProcessed = Math.Max(_totalProcessed, processPairs.Count);

                        mainLayout["Processes"].Update(CreateProcessTable(processPairs));
                     }

                     mainLayout["Statistics"].Update(CreateStatisticsPanel(processPairs));
                     mainLayout["Log"].Update(CreateLogPanel());

                     mainLayout["Footer"].Update(
                        new Markup($"[grey]Press [blue]Ctrl+C[/] to exit • Refresh: {settings.RefreshInterval}s • Uptime: {FormatUptime()}[/]")
                     );

                     ctx.Refresh();

                     Task.Delay(settings.RefreshInterval * 1000).Wait();
                  }
               }
               finally
               {
                  _secondsTimer?.Dispose();
                  _secondsTimer = null;
               }
            });

         return 0;
      }
      catch (Exception ex)
      {
         if (!settings.QuietMode)
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");

         _secondsTimer?.Dispose();
         return 1;
      }
   }

   private Rows CreateStatusBar(int processCount, MonitorSettings settings)
   {
      var rule = new Rule("[blue]Process Tracker Monitor[/]")
          .RuleStyle("blue")
          .LeftJustified();

      var statusBar = new Columns(
          new Text($"Processes: {processCount}").LeftJustified(),
          new Text($"Refresh: {settings.RefreshInterval}s").Centered(),
          new Text($"Auto-exit: {(settings.AutoExitTimeout > 0 ? $"{settings.AutoExitTimeout}s" : "disabled")}").RightJustified()
      );

      return new(rule, statusBar);
   }

   private string FormatUptime()
   {
      var elapsed = _uptime.Elapsed;

      return elapsed.TotalHours >= 1
          ? $"{elapsed.Hours}h {elapsed.Minutes}m {elapsed.Seconds}s"
          : $"{elapsed.Minutes}m {elapsed.Seconds}s";
   }

   private Panel CreateStatisticsPanel(IReadOnlyList<ProcessPair> processPairs)
   {
      var active = 0;
      var mainOnly = 0;
      var childOnly = 0;
      var inactive = 0;

      foreach (var pair in processPairs)
      {
         var mainRunning = IsProcessRunning(pair.MainProcessId);
         var childRunning = IsProcessRunning(pair.ChildProcessId);

         if (mainRunning && childRunning)
            active++;
         else if (mainRunning)
            mainOnly++;
         else if (childRunning)
            childOnly++;
         else
            inactive++;
      }

      var grid = new Grid()
         .AddColumn(new GridColumn().NoWrap())
         .AddColumn(new GridColumn());

      grid.AddRow(new Markup("[green]+[/] Active Pairs:"), new Markup($"[green]{active}[/]"))
         .AddRow(new Markup("[blue]...[/] Main Only:"), new Markup($"[blue]{mainOnly}[/]"))
         .AddRow(new Markup("[yellow]...[/] Child Only:"), new Markup($"[yellow]{childOnly}[/]"))
         .AddRow(new Markup("[red]-[/] Inactive:"), new Markup($"[red]{inactive}[/]"))
         .AddRow(new Markup("Total Tracked:"), new Markup($"[grey]{_totalProcessed}[/]"))
         .AddRow(new Markup("Uptime:"), new Markup($"[grey]{FormatUptime()}[/]"));

      return new Panel(grid)
         .Header("[blue]Monitoring Stats[/]")
         .Expand();
   }

   private Panel CreateProcessTable(IReadOnlyList<ProcessPair> processPairs)
   {
      var table = new Table()
         .Title("[blue]Monitored Processes[/]")
         .Expand();

      table.AddColumns(
         new TableColumn("Main Process").Width(18),
         new TableColumn("Main ID").Width(8).Centered(),
         new TableColumn("Status").Width(8).Centered(),
         new TableColumn("Child Process").Width(18),
         new TableColumn("Child ID").Width(8).Centered(),
         new TableColumn("Added").Width(16).Centered()
      );

      foreach (var pair in processPairs)
      {
         var mainRunning = IsProcessRunning(pair.MainProcessId);
         var childRunning = IsProcessRunning(pair.ChildProcessId);

         string status;
         if (mainRunning && childRunning)
            status = "[green]+[/]";
         else if (mainRunning)
            status = "[blue]...[/]";
         else if (childRunning)
            status = "[yellow]...[/]";
         else
            status = "[red]-[/]";

         var mainName = TruncateName(pair.MainProcessName, 15);
         var childName = TruncateName(pair.ChildProcessName, 15);
         var time = pair.Time.ToString("yyyy-MM-dd HH:mm");

         table.AddRow(mainName, pair.MainProcessId.ToString(), status,
                     childName, pair.ChildProcessId.ToString(), time);
      }

      return new Panel(table)
         .Header("[blue]Monitored Processes[/]")
         .Expand();
   }

   private Panel CreateLogPanel()
   {
      var logContent = string.Join("\n", _logMessages.Reverse().Take(_maxLogMessages));

      return new Panel(new Markup(logContent))
         .Header("[blue]Event Log[/]")
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
