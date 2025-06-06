using ProcessTracker.Models;

namespace ProcessTracker.Processes;

/// <summary>
/// Manages single instance detection for the application
/// </summary>
public class SingleInstanceManager : IDisposable
{
   private const string DEFAULT_MUTEX_NAME = @"Global\ProcessTrackerSingleInstanceMutex";
   private readonly Mutex _mutex;
   private bool _mutexWasCreatedByUs;
   private bool _isDisposed;

   private readonly IProcessTrackerLogger _logger;

   /// <summary>
   /// Gets whether another instance is already running
   /// </summary>
   public bool IsAlreadyRunning { get; private set; }

   /// <summary>
   /// Creates a new single instance manager with the default mutex name
   /// </summary>
   /// <remarks>
   /// - <see cref="DEFAULT_MUTEX_NAME">default mutex name</see>
   /// </remarks>
   public SingleInstanceManager(IProcessTrackerLogger logger) : this(DEFAULT_MUTEX_NAME, logger) { }

   /// <summary>
   /// Creates a new single instance manager with a custom mutex name
   /// </summary>
   /// <param name="customMutexName">Name of the mutex to use for single instance detection</param>
   public SingleInstanceManager(string customMutexName, IProcessTrackerLogger logger)
   {
      _logger = logger;

      _mutex = new(true, customMutexName, out var createdNew);
      _mutexWasCreatedByUs = createdNew;

      IsAlreadyRunning = !createdNew;
   }

   /// <summary>
   /// Releases the mutex, allowing another instance to start
   /// </summary>
   public void Release()
   {
      if (!_isDisposed && _mutexWasCreatedByUs && !IsAlreadyRunning)
      {
         try
         {
            _mutex.ReleaseMutex();
            _mutexWasCreatedByUs = false;
            _logger.Info("Mutex released, allowing other instances to start");
         }
         catch (Exception ex)
         {
            _logger.Error($"Releasing mutex issue.\nDetails:\n{ex.Message}");
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
         var acquired = _mutex.WaitOne(0);

         if (acquired)
         {
            _mutexWasCreatedByUs = true;
            IsAlreadyRunning = false;
         }
         else
         {
            _mutexWasCreatedByUs = false;
            IsAlreadyRunning = true;
         }

         return _mutexWasCreatedByUs;
      }
      catch (Exception ex)
      {
         _logger.Error($"Acquiring mutex issue.\nDetails:\n{ex.Message}");
         return false;
      }
   }

   protected virtual void Dispose(bool disposing)
   {
      if (!_isDisposed && disposing)
      {
         try
         {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
         }
         catch { }

         _isDisposed = true;
      }
   }

   public void Dispose()
   {
      Dispose(true);
      GC.SuppressFinalize(this);
   }
}
