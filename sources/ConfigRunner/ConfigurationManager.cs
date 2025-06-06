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
        bool isSaved;

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
    public bool RemoveConfigurationFile(string fileName)
    {
        bool isRemoved;

        try
        {
            fileName = FileSystemUtilities.EnsureJsonExtension(fileName);
            var filePath = Path.Combine(ConfigurationDirectory, fileName);

            if (File.Exists(filePath))
                File.Delete(filePath);

            isRemoved = true;
        }
        catch
        {
            isRemoved = false;
        }

        return isRemoved;
    }

    /// <inheritdoc/>
    public bool RemoveAllConfigurationFiles()
    {
        bool isRemoved;

        try
        {
            if (Directory.Exists(ConfigurationDirectory))
            {
                var files = GetAllConfigurationFiles()
                    .Select(fileName => Path.Combine(ConfigurationDirectory, fileName ?? string.Empty));

                foreach (var file in files)
                    RemoveConfigurationFile(file);
            }

            isRemoved = true;
        }
        catch
        {
            isRemoved = false;
        }

        return isRemoved;
    }
    
    /// <summary>
    /// Removes the entire configuration directory, including all its subdirectories and files.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the directory was successfully removed or did not exist;
    /// <see langword="false"/> if an error occurred during removal (e.g., directory is in use, insufficient permissions).
    /// </returns>
    public bool RemoveAllConfigurationDirectories()
    {
        bool isRemoved;

        if (!Directory.Exists(ConfigurationDirectory))
        {
            isRemoved = false;
        }
        else
        {
            try
            {
                Directory.Delete(ConfigurationDirectory, true);
                isRemoved = true;
            }
            catch
            {
                isRemoved = false;
            }
        }
        
        return isRemoved;
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
    public IEnumerable<string?> GetAllConfigurationFiles()
    {
        IEnumerable<string?> defaultReturnValue = [null];
        
        try
        {
            if (!Directory.Exists(ConfigurationDirectory))
                return [null];

            return Directory.GetFiles(ConfigurationDirectory, $"*{ConfigurationConstants.CONFIG_EXTENSION}")
                ?.Select(Path.GetFileName)
                ?.Where(name => !string.IsNullOrEmpty(name)) ?? defaultReturnValue;
        }
        catch
        {
            return defaultReturnValue;
        }
    }
}