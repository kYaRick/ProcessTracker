using ProcessTracker.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ProcessTracker.Processes;

/// <summary>
/// Monitors parent-child process relationships and detects when parent processes terminate
/// </summary>
public class ProcessMonitor : IDisposable
{
   private readonly ConcurrentDictionary<int, MonitoredProcessInfo> _monitoredProcesses = new();
   private readonly CancellationTokenSource _cts = new();
   private Task? _monitoringTask;
   private readonly TimeSpan _checkInterval;
   private readonly IProcessTrackerLogger _logger;
   private bool _isDisposed;
   private volatile bool _isMonitoring;

   /// <summary>
   /// Occurs when a parent process has terminated and its child process should be terminated
   /// </summary>
   public event EventHandler<ProcessPair>? ProcessPairTerminated;

   /// <summary>
   /// Creates a new process monitor with the default check interval
   /// </summary>
   public ProcessMonitor(IProcessTrackerLogger logger)
       : this(TimeSpan.FromSeconds(5), logger) { }

   /// <summary>
   /// Creates a new process monitor with a custom check interval
   /// </summary>
   /// <param name="checkInterval">How frequently to check if processes are still running</param>
   public ProcessMonitor(TimeSpan checkInterval, IProcessTrackerLogger logger)
   {
      _checkInterval = checkInterval;
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      _logger.Info($"Process monitor created with check interval of {_checkInterval.TotalSeconds} seconds");
      StartMonitoringTask();
   }

   private void StartMonitoringTask()
   {
      _isMonitoring = true;
      _monitoringTask = Task.Run(async () =>
      {
         try
         {
            _logger.Info("Process monitoring task started");

            while (!_cts.Token.IsCancellationRequested)
            {
               await CheckProcessesAsync();
               await Task.Delay(_checkInterval, _cts.Token);
            }
         }
         catch (OperationCanceledException)
         {
            _logger.Info("Process monitoring task canceled");
         }
         catch (Exception ex)
         {
            _logger.Error($"Error in monitoring task: {ex}");
         }
         finally
         {
            _isMonitoring = false;
            _logger.Info("Process monitoring task stopped");
         }
      }, _cts.Token);
   }

   /// <summary>
   /// Gets whether the monitor is actively running
   /// </summary>
   public bool IsMonitoring => _isMonitoring && !_isDisposed;

   /// <summary>
   /// Starts monitoring a parent-child process pair
   /// </summary>
   /// <param name="pair">The process pair to monitor</param>
   /// <returns>True if monitoring was started successfully, false otherwise</returns>
   public bool StartMonitoring(ProcessPair pair)
   {
      if (pair is null || pair.MainProcessId <= 0 || pair.ChildProcessId <= 0)
      {
         _logger.Error("Cannot monitor null or invalid process pair");
         return false;
      }

      if (!TryGetProcess(pair.MainProcessId, out var mainProcess))
      {
         _logger.Warning($"Cannot monitor: Main process {pair.MainProcessId} not found");
         return false;
      }

      if (!TryGetProcess(pair.ChildProcessId, out var childProcess))
      {
         _logger.Warning($"Cannot monitor: Child process {pair.ChildProcessId} not found");
         return false;
      }

      var info = new MonitoredProcessInfo
      {
         Pair = pair,
         StartTime = DateTime.UtcNow
      };

      var added = _monitoredProcesses.TryAdd(pair.MainProcessId, info);

      if (added)
      {
         _logger.Info($"Started monitoring: {pair.MainProcessName} ({pair.MainProcessId}) -> {pair.ChildProcessName} ({pair.ChildProcessId})");

         try
         {
            if (mainProcess != null)
            {
               mainProcess.EnableRaisingEvents = true;
               mainProcess.Exited += (sender, e) =>
               {
                  _logger.Info($"Main process exit detected via event: {pair.MainProcessId}");
                  _ = CheckProcessesAsync();
               };
            }
         }
         catch (Exception ex)
         {
            _logger.Warning($"Could not register for process exit events: {ex.Message}");
         }
      }
      else
      {
         _logger.Warning($"Already monitoring process pair: {pair.MainProcessId} -> {pair.ChildProcessId}");
      }

      return added;
   }

   /// <summary>
   /// Stops monitoring a parent-child process pair
   /// </summary>
   /// <param name="pair">The process pair to stop monitoring</param>
   /// <returns>True if monitoring was stopped successfully, false otherwise</returns>
   public bool StopMonitoring(ProcessPair pair)
   {
      if (pair is null || pair.MainProcessId <= 0)
         return false;

      bool removed = _monitoredProcesses.TryRemove(pair.MainProcessId, out _);

      if (removed)
      {
         _logger.Info($"Stopped monitoring: {pair.MainProcessName} ({pair.MainProcessId}) -> {pair.ChildProcessName} ({pair.ChildProcessId})");
      }

      return removed;
   }

   /// <summary>
   /// Gets all currently monitored process pairs
   /// </summary>
   /// <returns>List of monitored process pairs</returns>
   public IReadOnlyList<ProcessPair> GetMonitoredProcesses()
   {
      var processes = _monitoredProcesses.Values
         .Select(info => info.Pair)
         .ToList();

      _logger.Info($"Retrieved {processes.Count} monitored process pairs");
      return processes;
   }

   private async Task CheckProcessesAsync()
   {
      if (_isDisposed)
         return;

      var processesToCheck = _monitoredProcesses.ToArray();
      _logger.Info($"Checking {processesToCheck.Length} monitored processes");

      foreach (var kvp in processesToCheck)
      {
         var mainProcessId = kvp.Key;
         var info = kvp.Value;
         var pair = info.Pair;

         var mainProcessRunning = IsProcessRunning(pair.MainProcessId);

         if (!mainProcessRunning)
         {
            _logger.Info($"Main process not running: {pair.MainProcessName} ({pair.MainProcessId})");

            var childProcessRunning = IsProcessRunning(pair.ChildProcessId);

            if (childProcessRunning)
            {
               _logger.Warning($"Main process terminated, child still running: {pair.ChildProcessName} ({pair.ChildProcessId})");

               try
               {
                  ProcessPairTerminated?.Invoke(this, pair);
                  _logger.Info($"Fired ProcessPairTerminated event for: {pair.MainProcessId} -> {pair.ChildProcessId}");
               }
               catch (Exception ex)
               {
                  _logger.Error($"Error firing ProcessPairTerminated event: {ex.Message}");
               }

               await TerminateProcessAsync(pair.ChildProcessId);
            }
            else
            {
               _logger.Info($"Child process already terminated: {pair.ChildProcessName} ({pair.ChildProcessId})");
            }

            if (_monitoredProcesses.TryRemove(mainProcessId, out _))
            {
               _logger.Info($"Removed terminated pair from monitoring: {pair.MainProcessId} -> {pair.ChildProcessId}");
            }
         }
         else
         {
            _logger.Info($"Main process still running: {pair.MainProcessName} ({pair.MainProcessId})");
         }
      }
   }

   private bool IsProcessRunning(int processId)
   {
      return TryGetProcess(processId, out _);
   }

   private bool TryGetProcess(int processId, out Process? process)
   {
      process = null;
      try
      {
         process = Process.GetProcessById(processId);
         return !process.HasExited;
      }
      catch (ArgumentException)
      {
         return false;
      }
      catch (InvalidOperationException)
      {
         return false;
      }
      catch (Exception ex)
      {
         _logger.Error($"Error checking process {processId}: {ex.Message}");
         return false;
      }
   }

   private async Task TerminateProcessAsync(int processId)
   {
      try
      {
         if (TryGetProcess(processId, out var process) && process != null)
         {
            _logger.Info($"Attempting to gracefully terminate process {process.ProcessName} (ID: {processId})");

            if (process.CloseMainWindow())
            {
               _logger.Info($"Sent close signal to process {processId}, waiting for exit");

               var exited = await Task.Run(() => process.WaitForExit(3000));

               if (!exited)
               {
                  _logger.Warning($"Process {processId} did not terminate gracefully, forcing termination");
                  process.Kill();
                  _logger.Info($"Process {processId} killed");
               }
               else
               {
                  _logger.Info($"Process {processId} terminated gracefully");
               }
            }
            else
            {
               _logger.Warning($"Cannot close main window for process {processId}, forcing termination");
               process.Kill();
               _logger.Info($"Process {processId} killed");
            }
         }
         else
         {
            _logger.Info($"Process {processId} already terminated");
         }
      }
      catch (Exception ex)
      {
         _logger.Error($"Error terminating process {processId}: {ex.Message}");
      }
   }

   protected virtual void Dispose(bool disposing)
   {
      if (!_isDisposed)
      {
         if (disposing)
         {
            _logger.Info("Disposing process monitor");

            try
            {
               _cts.Cancel();
               _logger.Info("Cancelled monitoring task");

               if (_monitoringTask != null && !_monitoringTask.IsCompleted)
               {
                  _logger.Info("Waiting for monitoring task to complete");
                  Task.WaitAny(new[] { _monitoringTask }, 1000);
               }
            }
            catch (Exception ex)
            {
               _logger.Error($"Error during monitor disposal: {ex.Message}");
            }
            finally
            {
               _cts.Dispose();
            }

            _logger.Info("Process monitor disposed");
         }

         _isDisposed = true;
      }
   }

   public void Dispose()
   {
      Dispose(true);
      GC.SuppressFinalize(this);
   }
}

