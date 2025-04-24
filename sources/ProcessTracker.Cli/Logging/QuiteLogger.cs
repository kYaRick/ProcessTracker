using ProcessTracker.Models;

namespace ProcessTracker.Cli.Logging;

public class QuiteLogger : IProcessTrackerLogger
{
   public void Error(string message) { }
   public void Info(string message) { }
   public void Warning(string message) { }
}

