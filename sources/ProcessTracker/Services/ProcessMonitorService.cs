using ProcessTracker.Models;
using ProcessTracker.Processes;
using System.Diagnostics;

namespace ProcessTracker.Services;

/// <summary>
/// Service that coordinates process monitoring and persistence
/// </summary>
public class ProcessMonitorService : IDisposable
{
   private static ProcessMonitorService? _instance = null;
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

   /// <inheritdoc cref="ProcessMonitorService"/>
   public ProcessMonitorService(IProcessTrackerLogger logger)
   {
      if (_singleInstance?.IsAlreadyRunning ?? false)
         return;

      _logger = logger;
      _singleInstance = new(_logger);
      _monitor = new(_logger);
      _repository = new();
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
      _repository?.LoadAll();

      if (_singleInstance?.IsAlreadyRunning ?? false)
         return;

      _monitor = monitor;
      _repository = repository;
      _singleInstance = singleInstance;
      _logger = logger;

      _monitor.ProcessPairTerminated += OnProcessPairTerminated;

      KeepInstanceAlive();
   }

   private void LoadAndStartMonitoring()
   {
      var pairs = _repository.LoadAll();
      foreach (var pair in pairs)
      {
         if (IsProcessRunning(pair.MainProcessId) && IsProcessRunning(pair.ChildProcessId))
            _monitor.StartMonitoring(pair);
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
      if (IsAlreadyRunning)
         return false;

      var mainProcessName = GetProcessName(mainProcessId);
      var childProcessName = GetProcessName(childProcessId);

      if (string.IsNullOrWhiteSpace(mainProcessName) || string.IsNullOrWhiteSpace(childProcessName))
         return false;

      var pair = new ProcessPair
      {
         MainProcessId = mainProcessId,
         MainProcessName = mainProcessName,
         ChildProcessId = childProcessId,
         ChildProcessName = childProcessName
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

   /// <summary>
   /// Removes a process pair from monitoring
   /// </summary>
   /// <param name="mainProcessId">ID of the parent process</param>
   /// <param name="childProcessId">ID of the child process</param>
   /// <returns>True if the pair was removed successfully, false otherwise</returns>
   public bool RemoveProcessPair(int mainProcessId, int childProcessId)
   {
      if (IsAlreadyRunning)
         return false;

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

   /// <summary>
   /// Gets all process pairs being monitored
   /// </summary>
   /// <returns>List of all monitored process pairs</returns>
   public IReadOnlyList<ProcessPair> GetAllProcessPairs()
   {
      if (IsAlreadyRunning)
         return Array.Empty<ProcessPair>();

      return _monitor.GetMonitoredProcesses();
   }

   /// <summary>
   /// Shuts down the monitor service
   /// </summary>
   public void Shutdown()
   {
      if (_isDisposed)
         return;

      _monitor.ProcessPairTerminated -= OnProcessPairTerminated;
      _monitor.Dispose();
      _singleInstance.Dispose();
      _isDisposed = true;
   }

   private void OnProcessPairTerminated(object? sender, ProcessPair pair)
   {
      ///~> When a process pair is terminated, remove it from the repository.
      var allPairs = _repository.LoadAll();
      var pairToRemove = allPairs.FirstOrDefault(p =>
         p.MainProcessId == pair.MainProcessId && p.ChildProcessId == pair.ChildProcessId);

      if (pairToRemove is { })
      {
         allPairs.Remove(pairToRemove);
         _repository.SaveAll(allPairs);
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

   protected virtual void Dispose(bool disposing)
   {
      if (!_isDisposed)
         _isDisposed = true;
   }

   private void KeepInstanceAlive()
   {
      _instance = this;

      if (!_singleInstance.IsAlreadyRunning)
         LoadAndStartMonitoring();

      _logger.Info("ProcessMonitorService initialized");
   }

   public void Dispose()
   {
      Dispose(true);
      GC.SuppressFinalize(this);
   }
}
