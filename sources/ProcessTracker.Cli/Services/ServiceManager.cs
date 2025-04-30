using ProcessTracker.Cli.Logging;
using ProcessTracker.Models;
using ProcessTracker.Processes;
using ProcessTracker.Services;

namespace ProcessTracker.Cli.Services;

/// <summary>
/// Manages a single instance of the ProcessMonitorService
/// </summary>
public static class ServiceManager
{
   private static ProcessMonitorService? _serviceInstance;
   private static bool _wasInstanceRunning = false;
   private static readonly object _lock = new();
   private static ProcessRepository? _repository;

   /// <summary>
   /// Gets or creates the singleton ProcessMonitorService instance
   /// </summary>
   /// <param name="quietMode">Whether to suppress output</param>
   /// <param name="customLogger">Optional custom logger to use</param>
   /// <param name="cleanupInvalidProcesses">Whether to clean up invalid processes from storage during initialization</param>
   /// <returns>The service instance and whether it was newly created</returns>
   public static (ProcessMonitorService Service, bool WasCreated) GetOrCreateService(
      bool quietMode = false,
      IProcessTrackerLogger? customLogger = null,
      bool cleanupInvalidProcesses = true)
   {
      lock (_lock)
      {
         if (_serviceInstance is { } && !_serviceInstance.IsAlreadyRunning)
            return (_serviceInstance, false);

         if (_serviceInstance is { })
         {
            try { _serviceInstance.Dispose(); }
            catch { }
            _serviceInstance = null;
         }

         IProcessTrackerLogger logger = customLogger ??
            (quietMode ? new QuiteLogger() : new CliLogger());

         var monitor = new ProcessMonitor(TimeSpan.FromSeconds(4), logger);
         _repository = new ProcessRepository();
         var singleInstance = new SingleInstanceManager(logger);

         _serviceInstance = new ProcessMonitorService(
             monitor, _repository, singleInstance, logger);

         return (_serviceInstance, true);
      }
   }

   /// <summary>
   /// Temporarily stops the service instance to perform an operation, then restores it
   /// </summary>
   /// <param name="action">The action to perform while the service is stopped</param>
   /// <param name="quietMode">Whether to suppress output</param>
   /// <param name="customLogger">Optional custom logger to use</param>
   /// <returns>The result of the action</returns>
   public static T WithTemporarilySuspendedService<T>(Func<ProcessMonitorService, T> action,
      bool quietMode = false,
      IProcessTrackerLogger? customLogger = null)
   {
      lock (_lock)
      {
         _wasInstanceRunning = false;

         var wasBackgroundRunning = BackgroundLauncher.IsBackgroundMonitorRunning();

         if (wasBackgroundRunning)
         {
            _wasInstanceRunning = true;
            BackgroundLauncher.TerminateBackgroundMonitor(customLogger);
         }

         var (service, _) = GetOrCreateService(quietMode, customLogger);
         service.ReleaseSingleInstanceLock();

         try
         {
            return action(service);
         }
         finally
         {
            service.TryAcquireSingleInstanceLock();

            if (_wasInstanceRunning)
            {
               BackgroundLauncher.LaunchBackgroundMonitor(3, 6, customLogger);
            }
         }
      }
   }
}
