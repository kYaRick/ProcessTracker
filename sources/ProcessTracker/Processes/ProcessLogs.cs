using ProcessTracker.Models;
using System.Diagnostics;

namespace ProcessTracker.Processes;

public class ProcessLogs : IProcessTrackerLogger
{
   public void Info(string message) =>
      Debug.WriteLine($"INFO: {message}");
   public void Warning(string message) =>
      Debug.WriteLine($"WARNING: {message}");
   public void Error(string message) =>
      Debug.WriteLine($"ERROR: {message}");
}