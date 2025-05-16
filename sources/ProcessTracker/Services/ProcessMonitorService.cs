using ProcessTracker.Models;
using ProcessTracker.Processes;
using System.Diagnostics;

namespace ProcessTracker.Services;

/// <summary>
/// Service that coordinates process monitoring and persistence
/// </summary>
public class ProcessMonitorService : IDisposable
{
   private readonly SingleInstanceManager _singleInstance;
   private readonly ProcessMonitor _monitor;
   private readonly ProcessRepository _repository;
   private readonly IProcessTrackerLogger _logger;
   private bool _isDisposed;

   /// <summary>
   /// Gets whether another instance of the service is already running
   /// </summary>
   public bool IsAlreadyRunning => _singleInstance.IsAlreadyRunning;

   /// <summary>
   /// Creates a new process monitor service with default dependencies
   /// </summary>
   public ProcessMonitorService() : this(new ProcessLogs()) { }

   /// <summary>
   /// Creates a new process monitor service with logger
   /// </summary>
   public ProcessMonitorService(IProcessTrackerLogger logger)
   {
      _logger = logger;
      _singleInstance = new(_logger);
      _monitor = new(_logger);
      _repository = new();

      if (!IsAlreadyRunning)
         LoadStoredProcesses();
   }

   /// <summary>
   /// Creates a new process monitor service with custom dependencies
   /// </summary>
   public ProcessMonitorService(
      ProcessMonitor monitor,
      ProcessRepository repository,
      SingleInstanceManager singleInstance,
      IProcessTrackerLogger logger)
   {
      _monitor = monitor;
      _repository = repository;
      _singleInstance = singleInstance;
      _logger = logger;

      _monitor.ProcessPairTerminated += OnProcessPairTerminated;

      LoadStoredProcesses();
   }

   /// <summary>
   /// Releases the single instance lock, allowing other instances to start
   /// </summary>
   public void ReleaseSingleInstanceLock()
   {
      _logger.Info("Releasing single instance lock");
      _singleInstance.Release();
   }

   /// <summary>
   /// Tries to reacquire the single instance lock
   /// </summary>
   /// <returns>True if successfully reacquired the lock</returns>
   public bool TryAcquireSingleInstanceLock()
   {
      _logger.Info("Attempting to acquire single instance lock");
      return _singleInstance.TryAcquire();
   }

   private void LoadStoredProcesses()
   {
      try
      {
         var allPairs = _repository.LoadAll();
         var validPairs = new List<ProcessPair>();

         foreach (var pair in allPairs)
         {
            if (IsProcessRunning(pair.MainProcessId) && IsProcessRunning(pair.ChildProcessId))
            {
               _monitor.StartMonitoring(pair);
               validPairs.Add(pair);
            }
            else
            {
               _logger.Info($"Skipping invalid process pair: {pair.MainProcessName}({pair.MainProcessId}) → {pair.ChildProcessName}({pair.ChildProcessId})");
            }
         }

         if (validPairs.Count < allPairs.Count)
         {
            _logger.Info($"Cleaning up {allPairs.Count - validPairs.Count} invalid process pairs");
            _repository.SaveAll(validPairs);
         }
      }
      catch (Exception ex)
      {
         _logger.Error($"Error loading stored processes: {ex.Message}");
      }
   }

   /// <summary>
   /// Adds a new process pair to be monitored
   /// </summary>
   /// <param name="mainProcessId">ID of the parent process</param>
   /// <param name="childProcessId">ID of the child process</param>
   /// <returns>True if the pair was added successfully, false otherwise</returns>
   public bool AddProcessPair(int mainProcessId, int childProcessId)
   {
      var needToReacquireLock = false;

      if (IsAlreadyRunning)
      {
         _singleInstance.Release();
         needToReacquireLock = true;
      }

      try
      {
         var mainProcessName = GetProcessName(mainProcessId);
         var childProcessName = GetProcessName(childProcessId);

         if (string.IsNullOrWhiteSpace(mainProcessName) || string.IsNullOrWhiteSpace(childProcessName))
            return false;

         var pair = new ProcessPair
         {
            MainProcessId = mainProcessId,
            MainProcessName = mainProcessName,
            ChildProcessId = childProcessId,
            ChildProcessName = childProcessName,
            Time = DateTime.UtcNow
         };

         var monitoringStarted = _monitor.StartMonitoring(pair);
         if (!monitoringStarted)
            return false;

         var allPairs = _repository.LoadAll();

         if (allPairs.Any(p => p.MainProcessId == mainProcessId && p.ChildProcessId == childProcessId))
            return true;

         allPairs.Add(pair);
         _repository.SaveAll(allPairs);

         return true;
      }
      finally
      {
         if (needToReacquireLock)
         {
            _singleInstance.TryAcquire();
         }
      }
   }

   /// <summary>
   /// Removes a process pair from monitoring
   /// </summary>
   /// <param name="mainProcessId">ID of the parent process</param>
   /// <param name="childProcessId">ID of the child process</param>
   /// <returns>True if the pair was removed successfully, false otherwise</returns>
   public bool RemoveProcessPair(int mainProcessId, int childProcessId)
   {
      var needToReacquireLock = false;

      if (IsAlreadyRunning)
      {
         _singleInstance.Release();
         needToReacquireLock = true;
      }

      try
      {
         var allPairs = _repository.LoadAll();
         var pairToRemove = allPairs.FirstOrDefault(p =>
            p.MainProcessId == mainProcessId && p.ChildProcessId == childProcessId);

         if (pairToRemove == null)
            return false;

         _monitor.StopMonitoring(pairToRemove);

         allPairs.Remove(pairToRemove);
         _repository.SaveAll(allPairs);

         return true;
      }
      finally
      {
         if (needToReacquireLock)
         {
            _singleInstance.TryAcquire();
         }
      }
   }

   /// <summary>
   /// Gets all process pairs being monitored
   /// </summary>
   /// <returns>List of all monitored process pairs</returns>
   public IReadOnlyList<ProcessPair> GetAllProcessPairs() =>
      _monitor.GetMonitoredProcesses();

   private void OnProcessPairTerminated(object? sender, ProcessPair pair)
   {
      var allPairs = _repository.LoadAll();
      var pairToRemove = allPairs.FirstOrDefault(p =>
         p.MainProcessId == pair.MainProcessId && p.ChildProcessId == pair.ChildProcessId);

      if (pairToRemove is { })
      {
         allPairs.Remove(pairToRemove);
         _repository.SaveAll(allPairs);
      }
   }

   /// <summary>
   /// Refreshes the monitor with the latest process pairs from the repository
   /// </summary>
   public void RefreshFromRepository()
   {
      try
      {
         var allPairs = _repository.LoadAll();
         var currentPairs = _monitor.GetMonitoredProcesses();

         foreach (var pair in allPairs)
         {
            if (!currentPairs.Any(p =>
                p.MainProcessId == pair.MainProcessId &&
                p.ChildProcessId == pair.ChildProcessId))
            {
               if (IsProcessRunning(pair.MainProcessId) && IsProcessRunning(pair.ChildProcessId))
               {
                  _monitor.StartMonitoring(pair);
                  _logger.Info($"Added new process pair during refresh: {pair.MainProcessName}({pair.MainProcessId}) → {pair.ChildProcessName}({pair.ChildProcessId})");
               }
            }
         }

         foreach (var currentPair in currentPairs)
         {
            if (!allPairs.Any(p =>
                p.MainProcessId == currentPair.MainProcessId &&
                p.ChildProcessId == currentPair.ChildProcessId))
            {
               _monitor.StopMonitoring(currentPair);
               _logger.Info($"Removed process pair during refresh: {currentPair.MainProcessName}({currentPair.MainProcessId}) → {currentPair.ChildProcessName}({currentPair.ChildProcessId})");
            }
         }
      }
      catch (Exception ex)
      {
         _logger.Error($"Error refreshing processes from repository: {ex.Message}");
      }
   }

   private bool IsProcessRunning(int processId)
   {
      try
      {
         var process = Process.GetProcessById(processId);
         return !process.HasExited;
      }
      catch
      {
         return false;
      }
   }

   private string GetProcessName(int processId)
   {
      try
      {
         var process = Process.GetProcessById(processId);
         return process.ProcessName;
      }
      catch
      {
         return string.Empty;
      }
   }

   public void Dispose()
   {
      if (!_isDisposed)
      {
         _monitor.ProcessPairTerminated -= OnProcessPairTerminated;
         _monitor.Dispose();
         _singleInstance.Dispose();
         _isDisposed = true;
      }

      GC.SuppressFinalize(this);
   }
}
