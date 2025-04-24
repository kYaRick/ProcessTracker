using ProcessTracker.Cli.Logging;
using ProcessTracker.Processes;
using ProcessTracker.Services;

namespace ProcessTracker.Cli.Services;

/// <summary>
/// Manages a single instance of the ProcessMonitorService to ensure continuity across commands
/// </summary>
public static class ServiceManager
{
   private static ProcessMonitorService? _serviceInstance;
   private static readonly object _lock = new();
   private static bool _initialized = false;

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
            try
            {
               _serviceInstance.Dispose();
            }
            catch
            {
            }
            _serviceInstance = null;
         }

         var logger = new CliLogger();
         var monitor = new ProcessMonitor(TimeSpan.FromSeconds(2), logger);
         var repository = new ProcessRepository();
         var singleInstance = new SingleInstanceManager(logger);

         _serviceInstance = new ProcessMonitorService(
             monitor, repository, singleInstance, logger);
         _initialized = true;

         return (_serviceInstance, true);
      }
   }

   /// <summary>
   /// Explicitly shuts down the service when the application is exiting
   /// </summary>
   public static void ShutdownService()
   {
      lock (_lock)
      {
         if (_serviceInstance == null)
            return;

         try
         {
            _serviceInstance.Shutdown();
         }
         finally
         {
            _serviceInstance = null;
            _initialized = false;
         }
      }
   }

   /// <summary>
   /// Returns whether the service is currently initialized
   /// </summary>
   public static bool IsServiceInitialized()
   {
      lock (_lock)
      {
         return _initialized && _serviceInstance != null;
      }
   }
}

