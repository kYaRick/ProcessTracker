
using ConfigRunner;
using ConfigRunner.Constants;
using ProcessTracker.Processes;

namespace ProcessTracker.Models;

public class Settings : IProcessTrackerSettings
{
   private ConfigurationManager _configurationManager = new(ConfigurationType.Local, string.Empty);
   public string DefaultMutexName { get; set; } = SingleInstanceManager.DEFAULT_MUTEX_NAME;

   public string SettingsPath { get; set; } = @"settings.json";

   public uint MaxProcesses { get; set; } = 20;
   public TimeSpan MutexAcquireTimeout { get; set; } = TimeSpan.FromSeconds(0);
   public TimeSpan CheckTimeout { get; set; } = TimeSpan.FromSeconds(3);
   public TimeSpan AutoCloseTimeout { get; set; } = TimeSpan.FromSeconds(6);
   public TimeSpan ProcessWaitTimeout { get; set; } = TimeSpan.FromSeconds(1);
   public TimeSpan ProcessGracefulTimeout { get; set; } = TimeSpan.FromSeconds(5);

   public string ConfigurationDirectory => _configurationManager.ConfigurationPath;

   public Settings() { }

   public void ReadSettings(out IProcessTrackerSettings settings)
   {
      var defaultSettingsObject = new Settings();
      var fullSettingsPath = Path.Combine(_configurationManager.ConfigurationPath, defaultSettingsObject.SettingsPath);
      var settingsObject = _configurationManager.ReadConfiguration<Settings>(fullSettingsPath);

      settings = settingsObject ?? defaultSettingsObject;
   }

   public bool SaveSettings(IProcessTrackerSettings settings)
   {
      if (settings is not Settings settingsObject)
         return false;

      var fullSettingsPath = Path.Combine(_configurationManager.ConfigurationPath, settingsObject.SettingsPath);

      return _configurationManager.SaveConfiguration(settingsObject, fullSettingsPath);
   }

   public bool RemoveSettings()
   {
      var fullSettingsPath = Path.Combine(ConfigurationDirectory, SettingsPath);
      return _configurationManager.RemoveConfigurationFile(fullSettingsPath);
   }

   public bool ResetToDefault(out IProcessTrackerSettings settings)
   {
      var fullSettingsPath = Path.Combine(ConfigurationDirectory, SettingsPath);

      RemoveSettings();
      ReadSettings(out settings);

      return File.Exists(fullSettingsPath);
   }
}
