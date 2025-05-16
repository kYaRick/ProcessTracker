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
   /// Performs an operation on the service while temporarily suspending the single instance lock
   /// </summary>
   /// <typeparam name="T">The return type of the operation</typeparam>
   /// <param name="operation">The operation to perform on the service</param>
   /// <param name="quietMode">Whether to operate in quiet mode</param>
   /// <param name="customLogger">Optional logger to use</param>
   /// <returns>The result of the operation</returns>
   public static T WithTemporarilySuspendedService<T>(
       Func<ProcessMonitorService, T> operation,
       bool quietMode = false,
       IProcessTrackerLogger? customLogger = null)
   {
      var (service, cleanup) = GetOrCreateService(quietMode, customLogger);

      var wasRunning = service.IsAlreadyRunning;

      if (wasRunning)
      {
         service.ReleaseSingleInstanceLock();
      }

      try
      {
         return operation(service);
      }
      finally
      {
         if (wasRunning)
         {
            service.TryAcquireSingleInstanceLock();
         }

         if (cleanup)
         {
            service.Dispose();
         }
      }
   }
}
