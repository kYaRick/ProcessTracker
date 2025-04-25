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
   [DefaultValue(0)]
   public int AutoExitTimeout { get; set; }

   [CommandOption("-v|--verbose")]
   [Description("Show verbose output including detailed process information")]
   [DefaultValue(false)]
   public bool Verbose { get; set; }

   public override ValidationResult Validate()
   {
      if (RefreshInterval <= 0)
         return ValidationResult.Error("Refresh interval must be greater than 0.");

      if (AutoExitTimeout < 0)
         return ValidationResult.Error("Auto-exit timeout must be greater than or equal to 0.");

      return ValidationResult.Success();
   }
}

/// <summary>
/// Settings for service command
/// </summary>
public class ServiceSettings : BasicCommandSettings
{
   [CommandOption("-c|--check-interval")]
   [Description("How often to check processes (in seconds)")]
   [DefaultValue(5)]
   public int CheckInterval { get; set; }

   [CommandOption("-a|--auto-shutdown")]
   [Description("Automatically shutdown monitoring after specified seconds with no processes (0 to disable)")]
   [DefaultValue(0)]
   public int AutoShutdownTimeout { get; set; }

   [CommandOption("-e|--auto-exit")]
   [Description("Automatically exit the service when auto-shutdown occurs")]
   [DefaultValue(false)]
   public bool AutoExit { get; set; }

   [CommandOption("-v|--verbose")]
   [Description("Show verbose output including detailed process information")]
   [DefaultValue(false)]
   public bool Verbose { get; set; }

   public override ValidationResult Validate()
   {
      if (CheckInterval <= 0)
         return ValidationResult.Error("Check interval must be greater than 0.");

      if (AutoShutdownTimeout < 0)
         return ValidationResult.Error("Auto-shutdown timeout must be greater than or equal to 0.");

      return ValidationResult.Success();
   }
}


/// <summary>
/// Settings for stop command
/// </summary>
public class StopSettings : BasicCommandSettings
{
   [CommandOption("-f|--force")]
   [Description("Force stop even if processes are still being monitored")]
   [DefaultValue(false)]
   public bool Force { get; set; }
}
