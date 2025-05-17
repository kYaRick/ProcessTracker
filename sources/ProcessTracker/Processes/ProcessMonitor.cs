using ProcessTracker.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ProcessTracker.Processes;

/// <summary>
/// Monitors parent-child process relationships and detects when parent processes terminate
/// </summary>
public class ProcessMonitor : IDisposable
{
   private readonly ConcurrentBag<ProcessPair> _monitoredProcesses = new();
   private readonly CancellationTokenSource _cts = new();
   private Task? _monitoringTask;
   private readonly TimeSpan _checkInterval;
   private readonly IProcessTrackerLogger _logger;
   private volatile bool _isMonitoring;
   private bool _isDisposed;

   /// <summary>
   /// Gets whether the monitor is actively running
   /// </summary>
   public bool IsMonitoring =>
      _isMonitoring && !_isDisposed;

   /// <summary>
   /// Occurs when a parent process has terminated and its child process should be terminated
   /// </summary>
   public event EventHandler<ProcessPair>? ProcessPairTerminated;

   /// <summary>
   /// Creates a new process monitor with the default check interval
   /// </summary>
   /// <remarks>
   /// Default check interval is 5 seconds.
   /// This can be changed by using the constructor that takes a check interval parameter.
   /// </remarks>
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
      _logger.Info("Process monitor created");
      StartMonitoringTask();
   }

   private void StartMonitoringTask()
   {
      _isMonitoring = true;
      _monitoringTask = Task.Run(async () =>
      {
         try
         {
            while (!_cts.Token.IsCancellationRequested)
            {
               await CheckProcessesAsync();
               await Task.Delay(_checkInterval, _cts.Token);
            }
         }
         catch (OperationCanceledException)
         {
         }
         catch (Exception ex)
         {
            _logger.Error($"Monitoring error: {ex.Message}");
         }
         finally
         {
            _isMonitoring = false;
         }
      }, _cts.Token);
   }

   /// <summary>
   /// Starts monitoring a parent-child process pair
   /// </summary>
   /// <param name="pair">The process pair to monitor</param>
   /// <returns>True if monitoring was started successfully, false otherwise</returns>
   public bool StartMonitoring(ProcessPair pair)
   {
      if (pair is null || pair.MainProcessId <= 0 || pair.ChildProcessId <= 0)
         return false;

      if (!TryGetProcess(pair.MainProcessId, out var mainProcess))
         return false;

      if (!TryGetProcess(pair.ChildProcessId, out var childProcess))
         return false;

      if (GetMonitoredProcesses().Any(p => p.MainProcessId == pair.MainProcessId && p.ChildProcessId == pair.ChildProcessId))
         return false;

      if (pair.Time == default)
         pair.Time = DateTime.UtcNow;

      _monitoredProcesses.Add(pair);
      _logger.Info($"Started monitoring: {pair.MainProcessId} -> {pair.ChildProcessId}");

      try
      {
         if (mainProcess is { })
         {
            mainProcess.EnableRaisingEvents = true;
            mainProcess.Exited += (sender, e) => _ = CheckProcessesAsync();
         }
      }
      catch
      {
      }

      return true;
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

      lock (_monitoredProcesses)
      {
         var currentProcesses = _monitoredProcesses.ToArray();
         _monitoredProcesses.Clear();

         var removed = false;

         foreach (var p in currentProcesses)
         {
            if (p.MainProcessId == pair.MainProcessId && p.ChildProcessId == pair.ChildProcessId)
            {
               removed = true;
               continue;
            }
            _monitoredProcesses.Add(p);
         }

         if (removed)
            _logger.Info($"Stopped monitoring: {pair.MainProcessId} -> {pair.ChildProcessId}");

         return removed;
      }
   }

   /// <summary>
   /// Gets all currently monitored process pairs
   /// </summary>
   /// <returns>List of monitored process pairs</returns>
   public IReadOnlyList<ProcessPair> GetMonitoredProcesses() =>
      _monitoredProcesses.ToList();

   private async Task CheckProcessesAsync()
   {
      if (_isDisposed)
         return;

      var processesToCheck = _monitoredProcesses.ToArray();
      var processesToRemove = new List<ProcessPair>();

      foreach (var pair in processesToCheck)
      {
         var mainProcessRunning = IsProcessRunning(pair.MainProcessId);
         var childProcessRunning = IsProcessRunning(pair.ChildProcessId);

         if (!mainProcessRunning && childProcessRunning)
         {
            try
            {
               _logger.Warning($"Main process {pair.MainProcessId} terminated, child {pair.ChildProcessId} still running");
               ProcessPairTerminated?.Invoke(this, pair);
            }
            catch { }

            await TerminateProcessAsync(pair.ChildProcessId);

            childProcessRunning = IsProcessRunning(pair.ChildProcessId);
            if (!childProcessRunning)
            {
               _logger.Info($"Pair {pair.MainProcessId} → {pair.ChildProcessId} fully terminated, removing from monitoring");
               processesToRemove.Add(pair);
            }
            else
            {
               _logger.Warning($"Child process {pair.ChildProcessId} couldn't be terminated, keeping pair in monitoring");
            }
         }
         else if (!mainProcessRunning && !childProcessRunning)
         {
            _logger.Info($"Both processes in pair {pair.MainProcessId} → {pair.ChildProcessId} terminated");
            processesToRemove.Add(pair);
         }
         else if (mainProcessRunning && !childProcessRunning)
         {
            _logger.Info($"Child process {pair.ChildProcessId} terminated while main process {pair.MainProcessId} is still running");
            _logger.Info($"Removing pair from monitoring: {pair.MainProcessName}:{pair.MainProcessId} → {pair.ChildProcessName}:{pair.ChildProcessId}");
            processesToRemove.Add(pair);
         }
      }

      if (processesToRemove.Count > 0)
      {
         lock (_monitoredProcesses)
         {
            var currentProcesses = _monitoredProcesses.Except(processesToRemove).ToArray();
            _monitoredProcesses.Clear();
            foreach (var p in currentProcesses)
               _monitoredProcesses.Add(p);
         }
      }
   }

   private bool IsProcessRunning(int processId) =>
      TryGetProcess(processId, out _);

   private bool TryGetProcess(int processId, out Process? process)
   {
      process = null;
      try
      {
         process = Process.GetProcessById(processId);
         return !process.HasExited;
      }
      catch
      {
         return false;
      }
   }

   private async Task TerminateProcessAsync(int processId)
   {
      Process? process = default;

      try
      {
         if (!TryGetProcess(processId, out process) || process is null || process.HasExited)
         {
            _logger.Warning($"Process {processId} not running.");
            return;
         }

         await CloseGracefullyAsync(process)
            .ConfigureAwait(false);
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("process has exited"))
      {
         _logger.Info($"Process {processId} already exited.");
      }
      catch
      {
         _logger.Error($"Failed to terminate {processId}.");
         throw;
      }
      finally
      {
         process?.Dispose();
      }
   }

   private async Task CloseGracefullyAsync(Process proc, int gracefulTimeoutMs = 5000)
   {
      if (proc.CloseMainWindow())
      {
         await WaitForExitAsync(proc, gracefulTimeoutMs);
         if (proc.HasExited)
         {
            _logger.Info($"Process {proc.Id} closed gracefully.");
            return;
         }

         _logger.Warning($"Process {proc.Id} is not responding to close request. Forcing termination.");
      }

      try
      {
         proc.Kill();
         await WaitForExitAsync(proc, 1000);
         _logger.Info($"Process {proc.Id} killed.");
      }
      catch (Exception ex)
      {
         _logger.Error($"Failed to kill process {proc.Id}: {ex.Message}");
      }
   }

   private async Task WaitForExitAsync(Process process, int timeout)
   {
      using var cts = new CancellationTokenSource(timeout);

      try
      {
         await process.WaitForExitAsync(cts.Token);
      }
      catch (OperationCanceledException)
      {

      }
   }

   public void Dispose()
   {
      if (!_isDisposed)
      {
         _logger.Info("Disposing process monitor");
         _cts.Cancel();

         try
         {
            if (_monitoringTask is { } && !_monitoringTask.IsCompleted)
               Task.WaitAny([_monitoringTask], 1000);
         }
         finally
         {
            _cts.Dispose();
         }

         _isDisposed = true;
      }

      GC.SuppressFinalize(this);
   }
}
