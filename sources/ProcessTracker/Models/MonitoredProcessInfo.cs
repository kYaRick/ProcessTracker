namespace ProcessTracker.Models;

/// <summary>
/// Internal class to store information about monitored processes
/// </summary>
internal class MonitoredProcessInfo
{
   public required ProcessPair Pair { get; init; }
   public DateTime StartTime { get; init; }
}
