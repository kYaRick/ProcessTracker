namespace ProcessTracker.Models;

/// <summary>
/// Represents an interface for process tracker settings.
/// This interface defines properties that control the tracker's behavior,
/// as well as methods for reading, saving, removing, and resetting settings.
/// </summary>
public interface IProcessTrackerSettings
{
   /// <summary>
   /// Gets or sets the path to the file or source where the settings are stored.
   /// </summary>
   string SettingsPath { get; set; }

   /// <summary>
   /// Gets or sets the default name of the mutex used for process synchronization.
   /// </summary>
   string DefaultMutexName { get; set; }

   /// <summary>
   /// Gets or sets the maximum allowed number of process instances.
   /// </summary>
   uint MaxProcesses { get; set; }

   /// <summary>
   /// Gets or sets the timeout for attempting to acquire the mutex.
   /// If the mutex cannot be acquired within this time, the operation is considered failed.
   /// </summary>
   TimeSpan MutexAcquireTimeout { get; set; }

   /// <summary>
   /// Gets or sets the time interval between consecutive process state checks.
   /// </summary>
   TimeSpan CheckTimeout { get; set; }

   /// <summary>
   /// Gets or sets the timeout after which a non-responsive process will be automatically closed.
   /// </summary>
   TimeSpan AutoCloseTimeout { get; set; }

   /// <summary>
   /// Gets or sets the timeout for waiting for a process to exit.
   /// </summary>
   TimeSpan ProcessWaitTimeout { get; set; }

   /// <summary>
   /// Gets or sets an additional "graceful" timeout for process termination.
   /// This timeout is provided after an attempt at normal termination, before forced termination is applied.
   /// </summary>
   TimeSpan ProcessGracefulTimeout { get; set; }

   /// <summary>
   /// Reads settings from the defined source and returns them.
   /// </summary>
   /// <param name="settings">An out parameter that will contain the loaded settings.</param>
   void ReadSettings(out IProcessTrackerSettings settings);

   /// <summary>
   /// Saves the provided settings to the defined source.
   /// </summary>
   /// <param name="settings">The settings object to save.</param>
   /// <returns><c>true</c> if the settings were successfully saved; otherwise, <c>false</c>.</returns>
   bool SaveSettings(IProcessTrackerSettings settings);

   /// <summary>
   /// Removes the settings from the defined source.
   /// </summary>
   /// <returns><c>true</c> if the settings were successfully removed; otherwise, <c>false</c>.</returns>
   bool RemoveSettings();

   /// <summary>
   /// Resets the settings to their default values and returns them.
   /// </summary>
   /// <param name="settings">An out parameter that will contain the default settings.</param>
   /// <returns><c>true</c> if the settings were successfully reset; otherwise, <c>false</c>.</returns>
   bool ResetToDefault(out IProcessTrackerSettings settings);
}