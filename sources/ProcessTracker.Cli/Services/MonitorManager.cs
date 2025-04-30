using ProcessTracker.Models;
using ProcessTracker.Processes;

namespace ProcessTracker.Cli.Services;

/// <summary>
/// Centralized manager for process monitoring that can run in the background
/// </summary>
public static class MonitorManager
{
   private static readonly object _lock = new();
   private static bool _isInitialized;
   private static IProcessTrackerLogger? _logger;

   private static int _refreshInterval;
   private static int _autoExitTimeout;

   /// <summary>
   /// Initializes the monitor manager with a logger
   /// </summary>
   public static void Initialize(IProcessTrackerLogger logger, int refreshInterval = 3, int autoExitTimeout = 6)
   {
      _refreshInterval = refreshInterval;
      _autoExitTimeout = autoExitTimeout;

      lock (_lock)
      {
         _logger = logger;
         _isInitialized = true;
      }
   }

   /// <summary>
   /// Ensures the background monitor is running
   /// </summary>
   public static bool EnsureBackgroundMonitorRunning()
   {
      if (!_isInitialized)
         Initialize(new ProcessLogs());

      return BackgroundLauncher.LaunchBackgroundMonitor(_refreshInterval, _autoExitTimeout, _logger);
   }

   /// <summary>
   /// Adds a process pair to be monitored and ensures the background monitor is running
   /// </summary>
   public static bool AddProcessPair(int mainProcessId, int childProcessId)
   {
      var success = ServiceManager.WithTemporarilySuspendedService(service =>
      {
         return service.AddProcessPair(mainProcessId, childProcessId);
      }, true, _logger);

      if (success)
         EnsureBackgroundMonitorRunning();

      return success;
   }

   /// <summary>
   /// Removes a process pair from monitoring
   /// </summary>
   public static bool RemoveProcessPair(int mainProcessId, int childProcessId) => ServiceManager.WithTemporarilySuspendedService(service =>
      {
         return service.RemoveProcessPair(mainProcessId, childProcessId);
      }, true, _logger);

   /// <summary>
   /// Gets all process pairs being monitored
   /// </summary>
   public static IReadOnlyList<ProcessPair> GetAllProcessPairs() => ServiceManager.WithTemporarilySuspendedService(service =>
      {
         return service.GetAllProcessPairs();
      }, true, _logger);

   /// <summary>
   /// Clears all monitored process pairs
   /// </summary>
   public static void ClearAllProcessPairs() =>
      ServiceManager.WithTemporarilySuspendedService(service =>
      {
         var repository = new ProcessRepository();
         repository.SaveAll(new List<ProcessPair>());
         return true;
      }, true, _logger);

   /// <summary>
   /// Terminates the background monitor process
   /// </summary>
   public static void TerminateBackgroundMonitor() =>
      BackgroundLauncher.TerminateBackgroundMonitor(_logger);
}

