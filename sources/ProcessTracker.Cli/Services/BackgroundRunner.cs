using ProcessTracker.Cli.Services;
using ProcessTracker.Models;
using ProcessTracker.Processes;

namespace ProcessTracker.Services
{
   /// <summary>
   /// A wrapper class that allows using ProcessTracker functionality in other projects
   /// through either direct API calls or by managing the background CLI process
   /// </summary>
   public class BackgroundRunner : IDisposable
   {
      private readonly IProcessTrackerLogger _logger;
      private bool _isDisposed;
      private readonly int _refreshInterval;
      private readonly int _autoExitTimeout;

      /// <summary>
      /// Gets whether the background monitor is currently running
      /// </summary>
      public bool IsRunning =>
         BackgroundLauncher.IsBackgroundMonitorRunning();

      /// <summary>
      /// Creates a new BackgroundRunner with specified settings
      /// </summary>
      /// <param name="logger">Optional logger for output messages</param>
      /// <param name="refreshInterval">Refresh interval in seconds (default: 3)</param>
      /// <param name="autoExitTimeout">Auto-exit timeout in intervals (0 to disable, default: 6)</param>
      public BackgroundRunner(IProcessTrackerLogger? logger = null, int refreshInterval = 3, int autoExitTimeout = 6)
      {
         _logger = logger ?? new ProcessLogs();
         _refreshInterval = Math.Max(1, refreshInterval);
         _autoExitTimeout = Math.Max(0, autoExitTimeout);

         MonitorManager.Initialize(_logger, _refreshInterval, _autoExitTimeout);
      }

      /// <summary>
      /// Starts the background monitor process if it's not already running
      /// </summary>
      /// <returns>True if started successfully or already running</returns>
      public bool Start()
      {
         if (IsRunning)
         {
            _logger.Info("Background monitor is already running");
            return true;
         }

         return BackgroundLauncher.LaunchBackgroundMonitor(_logger, _refreshInterval, _autoExitTimeout);
      }

      /// <summary>
      /// Stops the background monitor process if it's running
      /// </summary>
      /// <returns>True if stopped successfully or not running</returns>
      public bool Stop() =>
         BackgroundLauncher.TerminateBackgroundMonitor(_logger);

      /// <summary>
      /// Adds a process pair to be monitored and ensures the background monitor is running
      /// </summary>
      /// <param name="mainProcessId">ID of the main process</param>
      /// <param name="childProcessId">ID of the child process</param>
      /// <returns>True if added successfully</returns>
      public bool AddProcessPair(int mainProcessId, int childProcessId, bool isAutoRestart)
         => TryToRunSaveAction(
            MonitorManager.AddProcessPair,
            mainProcessId,
            childProcessId,
            isAutoRestart);

      /// <summary>
      /// Removes a process pair from monitoring
      /// </summary>
      /// <param name="mainProcessId">ID of the main process</param>
      /// <param name="childProcessId">ID of the child process</param>
      /// <returns>True if removed successfully</returns>
      public bool RemoveProcessPair(int mainProcessId, int childProcessId, bool isAutoRestart)
         => TryToRunSaveAction(
            MonitorManager.RemoveProcessPair,
            mainProcessId,
            childProcessId,
            isAutoRestart);

      /// <summary>
      /// Gets all process pairs being monitored
      /// </summary>
      /// <returns>List of all monitored process pairs</returns>
      public IReadOnlyList<ProcessPair> GetAllProcessPairs() =>
         MonitorManager.GetAllProcessPairs();

      /// <summary>
      /// Clears all monitored process pairs
      /// </summary>
      public void ClearAllProcessPairs() =>
         MonitorManager.ClearAllProcessPairs();

      /// <summary>
      /// Disposes resources and stops the background monitor
      /// </summary>
      public void Dispose()
      {
         if (!_isDisposed)
         {
            Stop();
            _isDisposed = true;
         }
         GC.SuppressFinalize(this);
      }

      private bool TryToRunSaveAction(
         Func<int, int, bool> action,
         int mainProcessId,
         int childProcessId,
         bool isAutoRestart)
      {
         if (BackgroundLauncher.IsBackgroundMonitorRunning() && isAutoRestart)
            Stop();

         var isSuccess = action(mainProcessId, childProcessId);

         if (isAutoRestart)
            isAutoRestart &= Start();

         return isSuccess;
      }
   }
}