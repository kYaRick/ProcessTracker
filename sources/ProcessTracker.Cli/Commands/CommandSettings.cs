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
   [Description("Refresh interval in seconds")]
   [DefaultValue(3)]
   public int RefreshInterval { get; set; }

   [CommandOption("-a|--auto-exit")]
   [Description("Automatically exit monitoring after specified seconds with no processes (0 to disable)")]
   [DefaultValue(6)]
   public int AutoExitTimeout { get; set; }

   public override ValidationResult Validate()
   {
      if (RefreshInterval < 1)
         return ValidationResult.Error("Refresh interval must be at least 1 second");

      if (AutoExitTimeout < 0)
         return ValidationResult.Error("Auto-exit timeout must be 0 or greater");

      return ValidationResult.Success();
   }
}
