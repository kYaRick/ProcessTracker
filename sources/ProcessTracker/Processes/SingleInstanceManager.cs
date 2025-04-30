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
   private bool _mutexWasCreatedByUs;
   private readonly string _mutexName;

   /// <summary>
   /// Gets whether another instance is already running
   /// </summary>
   public bool IsAlreadyRunning { get; private set; }

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
      _mutexName = mutexName;

      _mutex = new Mutex(true, mutexName, out var createdNew);
      _mutexWasCreatedByUs = createdNew;
      IsAlreadyRunning = !createdNew;
   }

   /// <summary>
   /// Releases the mutex, allowing another instance to start
   /// </summary>
   public void Release()
   {
      if (!IsAlreadyRunning && !_isDisposed && _mutexWasCreatedByUs)
      {
         try
         {
            _mutex.ReleaseMutex();
            _mutexWasCreatedByUs = false;
            _logger.Info("Mutex released, allowing other instances to start");
         }
         catch (Exception ex)
         {
            _logger.Error($"Error releasing mutex: {ex.Message}");
         }
      }
   }

   /// <summary>
   /// Tries to reacquire the mutex, preventing other instances from starting
   /// </summary>
   /// <returns>True if the mutex was successfully acquired</returns>
   public bool TryAcquire()
   {
      if (_isDisposed)
         return false;

      if (_mutexWasCreatedByUs)
         return true;

      try
      {
         bool acquired = _mutex.WaitOne(0);
         if (acquired)
         {
            _mutexWasCreatedByUs = true;
            IsAlreadyRunning = false;
            _logger.Info("Mutex acquired, preventing other instances");
            return true;
         }
         else
         {
            IsAlreadyRunning = true;
            return false;
         }
      }
      catch (Exception ex)
      {
         _logger.Error($"Error acquiring mutex: {ex.Message}");
         return false;
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
               if (_mutexWasCreatedByUs)
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
