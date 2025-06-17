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

   /// <summary>
   /// Gets the interval, in seconds, at which the system refreshes its state.
   /// </summary>
   /// <remarks>This property is read-only and can only be set internally. It is used to control the frequency
   /// of periodic updates.</remarks>
   public static int RefreshInterval { get; private set; }

   /// <summary>
   /// Gets the timeout duration, in seconds, before the application automatically exits.
   /// </summary>
   /// <remarks>This property is read-only and can only be set internally. It is used to control the frequency
   /// of periodic updates.</remarks>
   public static int AutoExitTimeout { get; private set; }

   /// <summary>
   /// Initializes the monitor manager with a logger
   /// </summary>
   /// <param name="logger">Logger to use for output messages</param>
   /// <param name="refreshInterval">Interval in seconds for refreshing the monitored processes</param>
   /// <param name="autoExitTimeout">Timeout in seconds for automatic exit</param>
   public static void Initialize(IProcessTrackerLogger logger, int refreshInterval, int autoExitTimeout)
   {
      RefreshInterval = refreshInterval;
      AutoExitTimeout = autoExitTimeout;

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
         Initialize(new ProcessLogs(), RefreshInterval, AutoExitTimeout);

      return BackgroundLauncher.LaunchBackgroundMonitor(_logger, RefreshInterval, AutoExitTimeout);
   }

   /// <summary>
   /// Adds a process pair to be monitored and ensures the background monitor is running
   /// </summary>
   /// <param name="mainProcessId">ID of the main process</param>
   /// <param name="childProcessId">Child process ID to monitor</param>
   public static bool AddProcessPair(int mainProcessId, int childProcessId) =>
      ServiceManager.WithTemporarilySuspendedService(service =>
            service.AddProcessPair(mainProcessId, childProcessId),
         quietMode: true,
         customLogger: _logger);

   /// <summary>
   /// Removes a process pair from monitoring
   /// </summary>
   /// <param name="mainProcessId">ID of the main process</param>
   /// <param name="childProcessId">Child process ID to monitor</param>
   public static bool RemoveProcessPair(int mainProcessId, int childProcessId) => ServiceManager.WithTemporarilySuspendedService(service =>
         service.RemoveProcessPair(mainProcessId, childProcessId),
      quietMode: true,
      customLogger: _logger);

   /// <summary>
   /// Gets all process pairs being monitored
   /// </summary>
   public static IReadOnlyList<ProcessPair> GetAllProcessPairs() => ServiceManager
      .WithTemporarilySuspendedService(
         service =>
            service.GetAllProcessPairs(),
         quietMode: true,
         customLogger: _logger);

   /// <summary>
   /// Clears all monitored process pairs
   /// </summary>
   public static void ClearAllProcessPairs() =>
      ServiceManager.WithTemporarilySuspendedService(service =>
         {
            var repository = new ProcessRepository();
            repository.SaveAll(new());
            return true;
         },
         quietMode: true,
         customLogger: _logger);

   /// <summary>
   /// Terminates the background monitor process
   /// </summary>
   public static void TerminateBackgroundMonitor() =>
      BackgroundLauncher.TerminateBackgroundMonitor(_logger);
}

