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
   private static readonly object _lock = new();

   /// <summary>
   /// Gets or creates the singleton ProcessMonitorService instance
   /// </summary>
   /// <param name="quietMode">Whether to suppress output</param>
   /// <returns>The service instance and whether it was newly created</returns>
   public static (ProcessMonitorService Service, bool WasCreated) GetOrCreateService(bool quietMode = false)
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

         IProcessTrackerLogger logger = quietMode ? new QuiteLogger() : new CliLogger();
         var monitor = new ProcessMonitor(TimeSpan.FromSeconds(4), logger);
         var repository = new ProcessRepository();
         var singleInstance = new SingleInstanceManager(logger);

         _serviceInstance = new ProcessMonitorService(
             monitor, repository, singleInstance, logger);

         return (_serviceInstance, true);
      }
   }

   /// <summary>
   /// Shuts down the service instance
   /// </summary>
   public static void ShutdownService()
   {
      lock (_lock)
      {
         if (_serviceInstance == null)
            return;

         try { _serviceInstance.Dispose(); }
         catch { }

         _serviceInstance = null;
      }
   }
}
