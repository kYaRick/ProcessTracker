namespace ConfigRunner.Constants;

/// <summary>
/// Defines different types of configuration storage
/// </summary>
public enum ConfigurationType
{
   Temp,
   Settings,
   Local
}

/// <summary>
/// Constants used throughout the configuration system
/// </summary>
public static class ConfigurationConstants
{
   /// <summary>
   /// Root directory name for all configuration files
   /// </summary>
   public const string APPLICATION_ROOT_DIR = "Process Tracker";
   /// <summary>
   /// Default JSON file extension
   /// </summary>
   public const string CONFIG_EXTENSION = ".json";
   /// <summary>
   /// Default empty JSON content
   /// </summary>
   public const string EMPTY_CONFIG = "{}";
}