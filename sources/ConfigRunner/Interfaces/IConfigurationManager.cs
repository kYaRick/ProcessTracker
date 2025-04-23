namespace ConfigRunner.Interfaces;

/// <summary>
/// Interface for configuration management operations
/// </summary>
public interface IConfigurationManager
{
   /// <summary>
   /// Root directory where configurations are stored
   /// </summary>
   string ConfigurationDirectory { get; }

   /// <summary>
   /// Reads a configuration from a file
   /// </summary>
   /// <typeparam name="T">ConfigurationRunner type</typeparam>
   /// <param name="fileName">Name of the configuration file</param>
   /// <returns>ConfigurationRunner object</returns>
   T? ReadConfiguration<T>(string fileName) where T : class, new();

   /// <summary>
   /// Saves a configuration to a file
   /// </summary>
   /// <typeparam name="T">ConfigurationRunner type</typeparam>
   /// <param name="configuration">ConfigurationRunner object to save</param>
   /// <param name="fileName">Name of the configuration file</param>
   /// <returns>True if save was successful</returns>
   bool SaveConfiguration<T>(T configuration, string fileName) where T : class;

   /// <summary>
   /// Checks if a configuration exists
   /// </summary>
   /// <param name="fileName">Name of the configuration file</param>
   /// <returns>True if configuration exists</returns>
   bool ConfigurationExists(string fileName);

   /// <summary>
   /// Deletes a configuration
   /// </summary>
   /// <param name="fileName">Name of the configuration file</param>
   /// <returns>True if deletion was successful</returns>
   bool DeleteConfiguration(string fileName);

   /// <summary>
   /// Clears all configurations
   /// </summary>
   /// <returns>True if clearing was successful</returns>
   bool ClearAllConfigurations();

   /// <summary>
   /// Sets a default configuration if one doesn't exist
   /// </summary>
   /// <typeparam name="T">ConfigurationRunner type</typeparam>
   /// <param name="defaultConfiguration">Default configuration</param>
   /// <param name="fileName">Name of the configuration file</param>
   /// <returns>True if operation was successful</returns>
   bool SetDefaultConfiguration<T>(T defaultConfiguration, string fileName) where T : class;

   /// <summary>
   /// Gets all configuration files
   /// </summary>
   /// <returns>List of configuration files</returns>
   IEnumerable<string> GetAllConfigurationFiles();
}
