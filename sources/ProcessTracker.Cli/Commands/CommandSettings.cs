using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Base settings for commands that require process pair IDs
/// </summary>
public class ProcessPairSettings : CommandSettings
{
   [CommandOption("-m|--main")]
   [Description("ID of the main (parent) process")]
   public int MainProcessId { get; set; }

   [CommandOption("-c|--child")]
   [Description("ID of the child (dependent) process")]
   public int ChildProcessId { get; set; }

   [CommandOption("-q|--quiet")]
   [Description("Suppress detailed output")]
   [DefaultValue(false)]
   public bool QuietMode { get; set; }

   public override ValidationResult Validate()
   {
      if (MainProcessId <= 0)
         return ValidationResult.Error("Main process ID must be greater than 0.");

      if (ChildProcessId <= 0)
         return ValidationResult.Error("Child process ID must be greater than 0.");

      if (MainProcessId == ChildProcessId)
         return ValidationResult.Error("Main and child process IDs cannot be the same.");

      return ValidationResult.Success();
   }
}

/// <summary>
/// Settings for commands that don't require process pair information
/// </summary>
public class BasicCommandSettings : CommandSettings
{
   [CommandOption("-q|--quiet")]
   [Description("Suppress detailed output")]
   [DefaultValue(false)]
   public bool QuietMode { get; set; }
}

/// <summary>
/// Settings for monitor command with additional options
/// </summary>
public class MonitorSettings : BasicCommandSettings
{
   [CommandOption("-i|--interval")]
   [Description("Refresh interval in seconds. Affects how often UI updates and defines auto-exit time units")]
   [DefaultValue(3)]
   public int RefreshInterval { get; set; }

   [CommandOption("-a|--auto-exit")]
   [Description("Auto-exit after N seconds with no processes (calculated as RefreshInterval × this value, 0 to disable)")]
   [DefaultValue(6)]
   public int AutoExitTimeout { get; set; }

   [CommandOption("-b|--background")]
   [Description("Run in background mode without visible console window")]
   [DefaultValue(false)]
   public bool BackgroundMode { get; set; }

   public override ValidationResult Validate()
   {
      if (RefreshInterval < 1)
         return ValidationResult.Error("Refresh interval must be at least 1 second");

      if (AutoExitTimeout < 0)
         return ValidationResult.Error("Auto-exit timeout must be 0 or greater");

      return ValidationResult.Success();
   }

   /// <summary>
   /// Settings for the process snapshot command
   /// </summary>
   public class ProcessSnapshotCommandSettings : BasicCommandSettings
   {
      [CommandOption("--before")]
      [Description("Take a snapshot of processes before an operation")]
      [DefaultValue(false)]
      public bool Before { get; set; }

      [CommandOption("--after")]
      [Description("Take a snapshot of processes after an operation and close any new instances")]
      [DefaultValue(false)]
      public bool After { get; set; }

      [CommandOption("-p|--process")]
      [Description("Specify the process name to track (e.g., 'WINWORD' for Word)")]
      public string? ProcessName { get; set; }
   }
}
