using ConfigRunner;
using ConfigRunner.Constants;
using ConfigRunner.Interfaces;
using ProcessTracker.Models;


namespace ProcessTracker.Processes;


/// <summary>
/// Manages persistence of process pairs using ConfigRunner for storage.
/// This repository handles saving and loading process tracking information.
/// </summary>
public class ProcessRepository
{
   /// <summary>
   /// The configuration manager used to persist process pairs
   /// </summary>
   private readonly IConfigurationManager _configManager;

   public ProcessRepository() :
      this(new ConfigurationManager(ConfigurationType.Temp, nameof(ProcessTracker)))
   { }

   /// <summary>
   /// Initializes a new instance of the <see cref="ProcessRepository"/> class.
   /// </summary>
   /// <param name="configManager">The configuration manager to use for persistence.</param>
   public ProcessRepository(IConfigurationManager configManager)
   {
      if (configManager is null)
         throw new ArgumentNullException(nameof(configManager), "Configuration manager cannot be null");

      _configManager = configManager;
   }

   /// <summary>
   /// Gets or sets the file name used to store process tracking configuration
   /// </summary>
   /// <remarks>
   /// By default, uses "tracker.json" as the configuration file name
   /// </remarks>
   public string ConfigurationFileName { get; internal set; } =
      $"tracker{ConfigurationConstants.CONFIG_EXTENSION}";

   /// <summary>
   /// Loads all process pairs from the configuration storage
   /// </summary>
   /// <returns>A list of all stored process pairs, or an empty list if none exist</returns>
   public List<ProcessPair> LoadAll() =>
      _configManager.ReadConfiguration<List<ProcessPair>>(ConfigurationFileName) ?? new();

   /// <summary>
   /// Saves a list of process pairs to the configuration storage
   /// </summary>
   /// <param name="pairs">The process pairs to save</param>
   /// <returns>True if the save operation was successful</returns>
   public void SaveAll(List<ProcessPair> pairs) =>
      _configManager.SaveConfiguration(pairs, ConfigurationFileName);

   /// <summary>
   /// Checks if any process pairs are currently stored in the repository
   /// </summary>
   /// <returns>True if at least one process pair exists, otherwise false</returns>
   public bool HasAny() =>
      LoadAll().Any();

   /// <summary>
   /// Clears all directories used for configuration storage.
   /// </summary>
   public void Clear() =>
      _configManager.RemoveAllConfigurationDirectories();
}
