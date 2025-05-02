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
   private static readonly Lock _lock = new();
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
         var singleInstance = new SingleInstanceManager(logger);
         _repository = new();

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
   /// <param name="terminateBackgroundProcess">Whether to terminate the background process</param>
   /// <param name="customLogger">Optional custom logger to use</param>
   /// <returns>The result of the action</returns>
   public static T WithTemporarilySuspendedService<T>(
   Func<ProcessMonitorService, T> action,
   bool quietMode = false,
   bool terminateBackgroundProcess = false,
   IProcessTrackerLogger? customLogger = null)
   {
      T result = default!;

      lock (_lock)
      {
         _wasInstanceRunning = false;

         var wasBackgroundRunning = BackgroundLauncher.IsBackgroundMonitorRunning();

         customLogger?.Info($"Background monitor running: {wasBackgroundRunning}");

         if (wasBackgroundRunning)
         {
            _wasInstanceRunning = true;

            if (terminateBackgroundProcess)
            {
               customLogger?.Info("Terminating background monitor");
               BackgroundLauncher.TerminateBackgroundMonitor(customLogger);
               Thread.Sleep(1000);
            }
         }

         var (service, _) = GetOrCreateService(quietMode, customLogger);

         service.ReleaseSingleInstanceLock();

         try
         {
            customLogger?.Info("Executing requested action");
            result = action.Invoke(service);
         }
         finally
         {
            var lockAcquired = service.TryAcquireSingleInstanceLock();
            customLogger?.Info($"Re-acquired lock: {lockAcquired}");

            if (_wasInstanceRunning && terminateBackgroundProcess)
            {
               customLogger?.Info("Restarting background monitor");
               var success = BackgroundLauncher.LaunchBackgroundMonitor(
                  customLogger,
                  MonitorManager.RefreshInterval,
                  MonitorManager.AutoExitTimeout);

               customLogger?.Info($"Background monitor restart {(success ? "succeeded" : "failed")}");
            }
         }

         return result;
      }
   }
}
