using ProcessTracker.Cli.Logging;
using ProcessTracker.Processes;
using ProcessTracker.Services;

namespace ProcessTracker.Cli.Services;

/// <summary>
/// Manages ProcessTrackerService instances to ensure they continue running across commands
/// </summary>
public static class ServiceManager
{
   private static ProcessMonitorService? _serviceInstance;
   private static readonly object _lock = new();

   /// <summary>
   /// Gets or creates the ProcessTrackerService singleton instance
   /// </summary>
   /// <param name="quietMode">Whether to suppress output messages</param>
   /// <returns>The service instance and whether it was newly created</returns>
   public static (ProcessMonitorService Service, bool WasCreated) GetOrCreateService(bool quietMode = false)
   {
      lock (_lock)
      {
         if (_serviceInstance != null)
            return (_serviceInstance, false);

         var logger = new CliLogger();
         var monitor = new ProcessMonitor(logger);
         var repository = new ProcessRepository();
         var singleInstance = new SingleInstanceManager(logger);

         _serviceInstance = new ProcessMonitorService(monitor, repository, singleInstance, logger);

         return (_serviceInstance, true);
      }
   }

   /// <summary>
   /// Checks if the service is already running in another process
   /// </summary>
   public static bool IsServiceRunningInAnotherProcess()
   {
      var (service, _) = GetOrCreateService(true);
      return service.IsAlreadyRunning;
   }

   /// <summary>
   /// Shuts down the service and releases resources
   /// </summary>
   public static void ShutdownService()
   {
      lock (_lock)
      {
         if (_serviceInstance == null)
            return;

         _serviceInstance.Shutdown();
         _serviceInstance = null;
      }
   }
}

