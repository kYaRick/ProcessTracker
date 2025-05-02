namespace ProcessTracker.Models;

/// <summary>
/// This interface defines a methods for handling logging messages in the ProcessTracker application.
/// </summary>
public interface IProcessTrackerLogger
{
   void Info(string message);
   void Warning(string message);
   void Error(string message);
}
