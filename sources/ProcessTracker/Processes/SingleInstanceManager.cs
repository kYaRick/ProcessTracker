using ProcessTracker.Models;

namespace ProcessTracker.Processes;

/// <summary>
/// Manages single instance detection for the application
/// </summary>
public class SingleInstanceManager : IDisposable
{
   private const string DEFAULT_MUTEX_NAME = "Global\\ProcessTrackerSingleInstanceMutex";
   private readonly Mutex _mutex;
   private readonly IProcessTrackerLogger _logger;
   private bool _isDisposed;

   /// <summary>
   /// Gets whether another instance is already running
   /// </summary>
   public bool IsAlreadyRunning { get; }

   /// <summary>
   /// Creates a new single instance manager with the default mutex name
   /// </summary>
   public SingleInstanceManager(IProcessTrackerLogger logger) : this(DEFAULT_MUTEX_NAME, logger) { }

   /// <summary>
   /// Creates a new single instance manager with a custom mutex name
   /// </summary>
   /// <param name="mutexName">Name of the mutex to use for single instance detection</param>
   public SingleInstanceManager(string mutexName, IProcessTrackerLogger logger)
   {
      _logger = logger;
      _mutex = new Mutex(true, mutexName, out bool createdNew);
      IsAlreadyRunning = !createdNew;
   }

   /// <summary>
   /// Releases the mutex, allowing another instance to start
   /// </summary>
   public void Release()
   {
      if (!IsAlreadyRunning && !_isDisposed)
      {
         try
         {
            _mutex.ReleaseMutex();
         }
         catch (Exception ex)
         {
            _logger.Error($"Releasing mutex: {ex.Message}");
         }
      }
   }

   protected virtual void Dispose(bool disposing)
   {
      if (!_isDisposed)
      {
         if (disposing)
         {
            try
            {
               if (!IsAlreadyRunning)
               {
                  _mutex.ReleaseMutex();
               }
               _mutex.Dispose();
            }
            catch { }
         }

         _isDisposed = true;
      }
   }

   public void Dispose()
   {
      Dispose(true);
      GC.SuppressFinalize(this);
   }
}
