using ProcessTracker.Models;

namespace ProcessTracker.Cli.Logging;

/// <summary>
/// Logger that writes to a file, useful for background processes
/// </summary>
public class FileLogger : IProcessTrackerLogger
{
   private readonly string _logFilePath;
   private readonly Lock _lock = new();

   public FileLogger()
   {
      var logDir = Path.Combine(Path.GetTempPath(), "ProcessTracker");
      Directory.CreateDirectory(logDir);
      _logFilePath = Path.Combine(logDir, "monitor.log");
   }

   public void Info(string message) =>
      WriteLog("INFO", message);

   public void Warning(string message) =>
      WriteLog("WARN", message);

   public void Error(string message) =>
      WriteLog("ERROR", message);

   private void WriteLog(string level, string message)
   {
      var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

      lock (_lock)
      {
         try
         {
            File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
         }
         catch
         {
         }
      }
   }
}
