namespace ProcessTracker.Models;

public class ProcessInfo
{
   public string MainProcessName { get; set; } = string.Empty;
   public int MainProcessId { get; set; } = 0;

   public string ChildProcessName { get; set; } = string.Empty;
   public int ChildProcessId { get; set; } = 0;
}
