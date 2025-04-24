namespace ProcessTracker.Models;

public interface IProcessTrackerLogger
{
   void Info(string message);
   void Warning(string message);
   void Error(string message);
}
