using ProcessTracker.Models;
using System.Diagnostics;
using System.Reflection;

namespace ProcessTracker.Cli.Services;

/// <summary>
/// Provides functionality to launch the application as a background process
/// </summary>
public static class BackgroundLauncher
{
   private static Process? _backgroundProcess;
   private static readonly object _lockObj = new object();

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
         if (_backgroundProcess is { } && !_backgroundProcess.HasExited)
         {
            logger?.Info("Background monitor is already running");
            return true;
         }

         try
         {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
               exePath = Assembly.GetEntryAssembly()?.Location;
               if (string.IsNullOrEmpty(exePath))
               {
                  logger?.Error("Could not determine executable path");
                  return false;
               }
            }

            var args = $"monitor --background --quiet --interval {refreshInterval} --auto-exit {autoExitTimeout}";

            var startInfo = new ProcessStartInfo
            {
               FileName = exePath,
               Arguments = args,
               UseShellExecute = true,
               CreateNoWindow = true,
               WindowStyle = ProcessWindowStyle.Hidden
            };

            _backgroundProcess = Process.Start(startInfo);

            if (_backgroundProcess == null)
            {
               logger?.Error("Failed to start background process");
               return false;
            }

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
         return _backgroundProcess != null && !_backgroundProcess.HasExited;
      }
   }

   /// <summary>
   /// Terminates the background monitor if it's running
   /// </summary>
   public static void TerminateBackgroundMonitor(IProcessTrackerLogger? logger = null)
   {
      lock (_lockObj)
      {
         try
         {
            if (_backgroundProcess != null && !_backgroundProcess.HasExited)
            {
               _backgroundProcess.Kill();
               _backgroundProcess.Dispose();
               logger?.Info("Background monitor terminated");
            }
            _backgroundProcess = null;
         }
         catch (Exception ex)
         {
            logger?.Error($"Error terminating background monitor: {ex.Message}");
         }
      }
   }
}
