using ConfigRunner;
using ConfigRunner.Constants;
using ProcessTracker.Models;
using System.Diagnostics;

namespace ProcessTracker.Services;

public class ProcessMonitor : IDisposable
{
   private const string MUTEX_NAME = "Global\\ProcessTrackerMutex";
   private readonly Timer _checkTimer;
   private readonly object _lockObject = new();
   private readonly ConfigurationManager _configManager;
   private readonly string _configFileName;
   private readonly Mutex _singleInstanceMutex;
   private bool _isDisposed;
   private static ProcessMonitor? _instance;

   public static ProcessMonitor Instance =>
      _instance ??= new();

   // Check if another instance is already running
   public bool IsAlreadyRunning { get; private set; }

   private ProcessMonitor(int checkIntervalMs = 5000)
   {
      _singleInstanceMutex = new(true, MUTEX_NAME, out bool createdNew);
      IsAlreadyRunning = !createdNew;

      if (IsAlreadyRunning)
      {
         Debug.WriteLine("Another instance of ProcessTracker is already running.");
         return; // Don't initialize the timer if we're not the primary instance
      }

      _configManager = new ConfigurationManager(ConfigurationType.Temp, nameof(ProcessMonitor));
      _configFileName = "process_list.json";
      _checkTimer = new Timer(CheckProcesses, null, checkIntervalMs, checkIntervalMs);

      Debug.WriteLine("Process monitoring service started");
   }

   public ProcessMonitor(ConfigurationType configurationType, string configFileName, int checkIntervalMs = 5000)
   {
      // This constructor should not be used for the singleton pattern
      // but is kept for compatibility with existing code
      _singleInstanceMutex = new Mutex(true, MUTEX_NAME, out bool createdNew);
      IsAlreadyRunning = !createdNew;

      if (IsAlreadyRunning)
      {
         Debug.WriteLine("Another instance of ProcessTracker is already running.");
         return;
      }

      _configManager = new ConfigurationManager(configurationType, nameof(ProcessMonitor));
      _configFileName = configFileName;
      _checkTimer = new Timer(CheckProcesses, null, checkIntervalMs, checkIntervalMs);
   }

   public bool AddProcessPair(string mainProcessName, int mainProcessId, string childProcessName, int childProcessId)
   {
      if (IsAlreadyRunning)
         return false;

      var processInfo = new ProcessInfo
      {
         MainProcessName = mainProcessName,
         MainProcessId = mainProcessId,
         ChildProcessName = childProcessName,
         ChildProcessId = childProcessId
      };

      lock (_lockObject)
      {
         // Check if we're already monitoring this pair
         var processList = ReadProcessList();
         if (processList.Any(p => p.MainProcessId == mainProcessId && p.ChildProcessId == childProcessId))
         {
            Debug.WriteLine($"Already monitoring process pair: {mainProcessId} -> {childProcessId}");
            return false;
         }

         processList.Add(processInfo);
         SaveProcessList(processList);
         Debug.WriteLine($"Added monitoring for {mainProcessName} ({mainProcessId}) -> {childProcessName} ({childProcessId})");
         return true;
      }
   }

   public bool RemoveProcessPair(int mainProcessId, int childProcessId)
   {
      if (IsAlreadyRunning)
         return false;

      lock (_lockObject)
      {
         var processList = ReadProcessList();
         var processToRemove = processList.FirstOrDefault(p =>
            p.MainProcessId == mainProcessId && p.ChildProcessId == childProcessId);

         if (processToRemove != null)
         {
            processList.Remove(processToRemove);
            SaveProcessList(processList);
            Debug.WriteLine($"Removed monitoring for {processToRemove.MainProcessName} ({mainProcessId}) -> {processToRemove.ChildProcessName} ({childProcessId})");
            return true;
         }
         return false;
      }
   }

   public void Shutdown()
   {
      // Clear all tracked processes
      lock (_lockObject)
      {
         var processList = ReadProcessList();
         processList.Clear();
         SaveProcessList(processList);
      }

      // Release the mutex so another instance can start if needed
      try
      {
         _singleInstanceMutex?.ReleaseMutex();
         Debug.WriteLine("Process tracker shutdown completed successfully.");
      }
      catch (Exception ex)
      {
         Debug.WriteLine($"Error during shutdown: {ex.Message}");
      }

      // Force this instance to stop monitoring
      _checkTimer?.Dispose();
      _isDisposed = true;
   }

   public List<ProcessInfo> GetTrackedProcesses()
   {
      if (IsAlreadyRunning)
         return new List<ProcessInfo>();

      lock (_lockObject)
      {
         return ReadProcessList();
      }
   }

   private void CheckProcesses(object? state)
   {
      if (IsAlreadyRunning)
         return;

      lock (_lockObject)
      {
         try
         {
            var processList = ReadProcessList();
            var processesToRemove = new List<ProcessInfo>();

            foreach (var processInfo in processList)
            {
               if (!IsProcessRunning(processInfo.MainProcessId, processInfo.MainProcessName))
               {
                  Debug.WriteLine($"Main process {processInfo.MainProcessName} ({processInfo.MainProcessId}) not found.");

                  if (IsProcessRunning(processInfo.ChildProcessId, processInfo.ChildProcessName))
                  {
                     Debug.WriteLine($"Terminating dependent process {processInfo.ChildProcessName} ({processInfo.ChildProcessId})...");
                     KillProcess(processInfo.ChildProcessId);
                  }

                  processesToRemove.Add(processInfo);
               }
            }

            foreach (var processInfo in processesToRemove)
            {
               processList.Remove(processInfo);
               Debug.WriteLine($"Removed process pair from monitoring: {processInfo.MainProcessName} -> {processInfo.ChildProcessName}");
            }

            if (processesToRemove.Count > 0)
               SaveProcessList(processList);

            // If there are no more processes to monitor, we can exit
            if (processList.Count == 0)
            {
               Debug.WriteLine("No more processes to monitor. You can safely exit the application.");
            }
         }
         catch (Exception ex)
         {
            Debug.WriteLine($"Error checking processes: {ex.Message}");
         }
      }
   }

   private bool IsProcessRunning(int processId, string expectedName)
   {
      try
      {
         var process = Process.GetProcessById(processId);
         return process.ProcessName.Equals(expectedName, StringComparison.OrdinalIgnoreCase);
      }
      catch
      {
         return false;
      }
   }

   private void KillProcess(int processId)
   {
      try
      {
         var process = Process.GetProcessById(processId);

         if (!process.HasExited)
         {
            Debug.WriteLine($"Attempting to gracefully close process {process.ProcessName} ({processId})...");

            if (process.CloseMainWindow())
            {
               if (!process.WaitForExit(3000))
               {
                  Debug.WriteLine($"Process did not close gracefully, forcing termination...");
                  process.Kill();
               }
               else
               {
                  Debug.WriteLine($"Process closed gracefully.");
               }
            }
            else
            {
               Debug.WriteLine($"Could not send close signal, forcing termination...");
               process.Kill();
            }
         }
      }
      catch (Exception ex)
      {
         Debug.WriteLine($"Error while terminating process {processId}: {ex.Message}");
      }
   }


   private List<ProcessInfo> ReadProcessList()
   {
      var processList = _configManager.ReadConfiguration<List<ProcessInfo>>(_configFileName);
      return processList ?? new List<ProcessInfo>();
   }

   private void SaveProcessList(List<ProcessInfo> processList) =>
      _configManager.SaveConfiguration(processList, _configFileName);

   public void Dispose()
   {
      if (_isDisposed)
         return;

      _checkTimer?.Dispose();

      if (!IsAlreadyRunning)
      {
         try
         {
            _singleInstanceMutex?.ReleaseMutex();
         }
         catch { }
      }

      _singleInstanceMutex?.Dispose();
      _isDisposed = true;
   }
}
