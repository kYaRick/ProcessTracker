using ConfigRunner.Constants;
using ConfigRunner.Interfaces;
using ConfigRunner.Utilities;

namespace ConfigRunner;

/// <summary>
/// Manages configuration files for applications
/// </summary>
public class ConfigurationManager : IConfigurationManager
{
   /// <summary>
   /// The root directory where configurations are stored
   /// </summary>
   public string ConfigurationDirectory { get; }

   /// <summary>
   /// Creates a new configuration manager
   /// </summary>
   /// <param name="configurationType">Type of configuration storage</param>
   /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
   /// <exception cref="Exception">Failed to create directory</exception>
   public ConfigurationManager(ConfigurationType configurationType)
   {
      var rootPath = FileSystemUtilities.GetRootPath(configurationType);

      ConfigurationDirectory = Path.Combine(rootPath, ConfigurationConstants.APPLICATION_ROOT_DIR);

      FileSystemUtilities.EnsureDirectoryExists(ConfigurationDirectory);
   }

   /// <inheritdoc cref="ConfigurationManager(ConfigurationType)"/>
   /// <param name="applicationName">Name of the application</param>
   public ConfigurationManager(ConfigurationType configurationType, string applicationName)
   {
      if (string.IsNullOrWhiteSpace(applicationName))
         throw new ArgumentException("Application name cannot be empty.", nameof(applicationName));

      var rootPath = FileSystemUtilities.GetRootPath(configurationType);

      ConfigurationDirectory = Path.Combine(rootPath, ConfigurationConstants.APPLICATION_ROOT_DIR, applicationName);

      FileSystemUtilities.EnsureDirectoryExists(ConfigurationDirectory);
   }

   /// <summary>
   /// Creates a new configuration manager with a custom configuration path
   /// </summary>
   /// <param name="configurationPath">Custom root path for configuration files</param>
   /// <param name="applicationName">Name of the application</param>
   /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
   /// <exception cref="Exception">Failed to create directory</exception>
   public ConfigurationManager(string configurationPath, string applicationName)
   {
      if (string.IsNullOrWhiteSpace(applicationName))
         throw new ArgumentException("Application name cannot be empty.", nameof(applicationName));

      if (string.IsNullOrWhiteSpace(configurationPath))
         throw new ArgumentException("ConfigurationRunner path cannot be empty.", nameof(configurationPath));

      if (!Directory.Exists(configurationPath))
         throw new ArgumentException($"Directory does not exist: {configurationPath}", nameof(configurationPath));

      ConfigurationDirectory = Path.Combine(configurationPath, applicationName);

      FileSystemUtilities.EnsureDirectoryExists(ConfigurationDirectory);
   }

   /// <inheritdoc/>
   public T? ReadConfiguration<T>(string fileName) where T : class, new()
   {
      try
      {
         fileName = FileSystemUtilities.EnsureJsonExtension(fileName);
         var filePath = Path.Combine(ConfigurationDirectory, fileName);

         FileSystemUtilities.EnsureJsonFileExists(filePath);

         var json = File.ReadAllText(filePath);
         return JsonSerializerUtilities.Deserialize<T>(json);
      }
      catch
      {
         return new T();
      }
   }

   /// <inheritdoc/>
   public bool SaveConfiguration<T>(T configuration, string fileName) where T : class
   {
      var isSaved = false;

      try
      {
         if (configuration is null)
            throw new ArgumentNullException(nameof(configuration));

         fileName = FileSystemUtilities.EnsureJsonExtension(fileName);
         var filePath = Path.Combine(ConfigurationDirectory, fileName);

         var directory = Path.GetDirectoryName(filePath);
         if (!string.IsNullOrWhiteSpace(directory))
            FileSystemUtilities.EnsureDirectoryExists(directory);

         var json = JsonSerializerUtilities.Serialize(configuration);
         File.WriteAllText(filePath, json);

         isSaved = true;
      }
      catch
      {
         isSaved = false;
      }

      return isSaved;
   }

   /// <inheritdoc/>
   public bool ConfigurationExists(string fileName)
   {
      fileName = FileSystemUtilities.EnsureJsonExtension(fileName);
      var filePath = Path.Combine(ConfigurationDirectory, fileName);

      return File.Exists(filePath);
   }

   /// <inheritdoc/>
   public bool DeleteConfiguration(string fileName)
   {
      var isDeleted = false;

      try
      {
         fileName = FileSystemUtilities.EnsureJsonExtension(fileName);
         var filePath = Path.Combine(ConfigurationDirectory, fileName);

         if (File.Exists(filePath))
            File.Delete(filePath);

         isDeleted = true;
      }
      catch
      {
         isDeleted = false;
      }

      return isDeleted;
   }

   /// <inheritdoc/>
   public bool ClearAllConfigurations()
   {
      var isCleaned = false;

      try
      {
         if (Directory.Exists(ConfigurationDirectory))
         {
            var files = GetAllConfigurationFiles()
               .Select(fileName => Path.Combine(ConfigurationDirectory, fileName))
               .ToList();

            foreach (var file in files)
               if (File.Exists(file))
                  File.Delete(file);
         }

         isCleaned = true;
      }
      catch
      {
         isCleaned = false;
      }

      return isCleaned;
   }

   /// <inheritdoc/>
   public bool SetDefaultConfiguration<T>(T defaultConfiguration, string fileName) where T : class
   {
      if (defaultConfiguration is null)
         throw new ArgumentNullException(nameof(defaultConfiguration));

      fileName = FileSystemUtilities.EnsureJsonExtension(fileName);

      if (ConfigurationExists(fileName))
         return true;

      return SaveConfiguration(defaultConfiguration, fileName);
   }

   /// <inheritdoc/>
   public IEnumerable<string> GetAllConfigurationFiles()
   {
      try
      {
         if (!Directory.Exists(ConfigurationDirectory))
            return Enumerable.Empty<string>();

         return Directory.GetFiles(ConfigurationDirectory, $"*{ConfigurationConstants.CONFIG_EXTENSION}")
             ?.Select(Path.GetFileName)
             ?.Where(name => !string.IsNullOrEmpty(name))!;
      }
      catch { return Enumerable.Empty<string>(); }
   }
}
