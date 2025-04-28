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
      // First make sure the background monitor is running
      if (!EnsureBackgroundMonitorRunning())
         return false;

      // Now add the process pair through the regular service
      var (service, _) = ServiceManager.GetOrCreateService(true);

      return service.AddProcessPair(mainProcessId, childProcessId);
   }

   /// <summary>
   /// Removes a process pair from monitoring
   /// </summary>
   public static bool RemoveProcessPair(int mainProcessId, int childProcessId)
   {
      var (service, _) = ServiceManager.GetOrCreateService(true);
      return service.RemoveProcessPair(mainProcessId, childProcessId);
   }

   /// <summary>
   /// Gets all process pairs being monitored
   /// </summary>
   public static IReadOnlyList<ProcessPair> GetAllProcessPairs()
   {
      var (service, _) = ServiceManager.GetOrCreateService(true);
      return service.GetAllProcessPairs();
   }

   /// <summary>
   /// Clears all monitored process pairs
   /// </summary>
   public static void ClearAllProcessPairs()
   {
      var (service, _) = ServiceManager.GetOrCreateService(true);
      var pairs = service.GetAllProcessPairs();

      foreach (var pair in pairs)
         service.RemoveProcessPair(pair.MainProcessId, pair.ChildProcessId);
   }

   /// <summary>
   /// Terminates the background monitor process
   /// </summary>
   public static void TerminateBackgroundMonitor()
   {
      BackgroundLauncher.TerminateBackgroundMonitor(_logger);
   }
}
