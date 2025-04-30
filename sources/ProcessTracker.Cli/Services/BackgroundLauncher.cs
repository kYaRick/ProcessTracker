using ConfigRunner;
using ConfigRunner.Constants;
using ProcessTracker.Models;
using System.Diagnostics;
using System.Reflection;

namespace ProcessTracker.Cli.Services;

/// <summary>
/// Provides functionality to launch the application as a background process
/// </summary>
public static class BackgroundLauncher
{
   private static ConfigurationManager _configManager = new(ConfigurationType.Temp, nameof(ProcessTracker));
   private static Process? _backgroundProcess;
   private static readonly object _lockObj = new object();
   private static readonly string _pidFilePath = Path.Combine(_configManager.ConfigurationDirectory, "background.log");

   /// <summary>
   /// Launches the application in background mode for monitoring
   /// </summary>
   /// <param name="refreshInterval">Refresh interval in seconds</param>
   /// <param name="autoExitTimeout">Auto-exit timeout in intervals (0 to disable)</param>
   /// <param name="logger">Logger to use for reporting</param>
   /// <returns>True if launched successfully, false otherwise</returns>
   public static bool LaunchBackgroundMonitor(int refreshInterval = 3, int autoExitTimeout = 6, IProcessTrackerLogger? logger = null)
   {
      lock (_lockObj)
      {
         if (IsBackgroundMonitorRunning())
         {
            logger?.Info("Background monitor is already running");
            return true;
         }

         try
         {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(exePath))
            {
               exePath = Assembly.GetEntryAssembly()?.Location;
               if (string.IsNullOrWhiteSpace(exePath))
               {
                  logger?.Error("Could not determine executable path");
                  return false;
               }
            }

            var args = $"monitor --quiet --interval {refreshInterval} --auto-exit {autoExitTimeout}";

            var startInfo = new ProcessStartInfo
            {
               FileName = exePath,
               Arguments = args,
               UseShellExecute = true,
               CreateNoWindow = true,
               WindowStyle = ProcessWindowStyle.Hidden
            };

            _backgroundProcess = Process.Start(startInfo);

            if (_backgroundProcess is null)
            {
               logger?.Error("Failed to start background process");
               return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_pidFilePath)!);

            File.WriteAllText(_pidFilePath, _backgroundProcess.Id.ToString());

            logger?.Info($"Background monitor launched (PID: {_backgroundProcess.Id})");
            return true;
         }
         catch (Exception ex)
         {
            logger?.Error($"Error launching background monitor: {ex.Message}");
            return false;
         }
      }
   }

   /// <summary>
   /// Checks if the background monitor is running
   /// </summary>
   public static bool IsBackgroundMonitorRunning()
   {
      lock (_lockObj)
      {
         if (_backgroundProcess is { })
         {
            try
            {
               if (!_backgroundProcess.HasExited)
                  return true;
            }
            catch
            {
               _backgroundProcess = null;
            }
         }

         try
         {
            if (File.Exists(_pidFilePath))
            {
               string pidContent = File.ReadAllText(_pidFilePath);
               if (int.TryParse(pidContent, out int pid))
               {
                  try
                  {
                     var process = Process.GetProcessById(pid);
                     _backgroundProcess = process;
                     return true;
                  }
                  catch
                  {
                     File.Delete(_pidFilePath);
                  }
               }
            }
         }
         catch { }

         return false;
      }
   }

   /// <summary>
   /// Terminates the background monitor if it's running
   /// </summary>
   /// <param name="logger">Optional logger</param>
   /// <returns>True if terminated successfully or already not running</returns>
   public static bool TerminateBackgroundMonitor(IProcessTrackerLogger? logger = null)
   {
      lock (_lockObj)
      {
         try
         {
            if (_backgroundProcess is { })
            {
               try
               {
                  if (!_backgroundProcess.HasExited)
                  {
                     _backgroundProcess.Kill();
                     logger?.Info($"Background monitor (PID: {_backgroundProcess.Id}) terminated");
                  }
                  _backgroundProcess.Dispose();
               }
               catch (Exception ex)
               {
                  logger?.Error($"Error terminating cached process: {ex.Message}");
               }

               _backgroundProcess = null;
            }

            if (File.Exists(_pidFilePath))
            {
               try
               {
                  string pidContent = File.ReadAllText(_pidFilePath);
                  if (int.TryParse(pidContent, out int pid))
                  {
                     try
                     {
                        var process = Process.GetProcessById(pid);
                        if (!process.HasExited)
                        {
                           process.Kill();
                           logger?.Info($"Background monitor (PID: {pid}) terminated from PID file");
                        }
                        process.Dispose();
                     }
                     catch (Exception ex)
                     {
                        logger?.Warning($"Could not terminate process with PID {pid}: {ex.Message}");
                     }
                  }
               }
               catch (Exception ex)
               {
                  logger?.Error($"Error reading PID file: {ex.Message}");
               }

               try
               {
                  File.Delete(_pidFilePath);
                  logger?.Info("PID file deleted");
               }
               catch (Exception ex)
               {
                  logger?.Warning($"Could not delete PID file: {ex.Message}");
               }
            }

            return true;
         }
         catch (Exception ex)
         {
            logger?.Error($"Error in TerminateBackgroundMonitor: {ex.Message}");
            return false;
         }
      }
   }
}
