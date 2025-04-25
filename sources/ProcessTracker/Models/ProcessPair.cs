namespace ProcessTracker.Models;

/// <summary>
/// Represents a pair of processes, including a main process and its associated child process.
/// </summary>
/// <remarks>This class is used to store information about a main process and its related child process, including
/// their names and process IDs. It can be useful for scenarios where relationships between processes need to be tracked
/// or managed.</remarks>
public class ProcessPair
{
   public DateTime Time { get; set; }

   public string MainProcessName { get; set; } = string.Empty;
   public int MainProcessId { get; set; } = 0;

   public string ChildProcessName { get; set; } = string.Empty;
   public int ChildProcessId { get; set; } = 0;
}
