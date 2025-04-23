using ConfigRunner.Constants;

namespace ConfigRunner.Utilities;

/// <summary>
/// Utilities for file system operations
/// </summary>
internal static class FileSystemUtilities
{
   /// <summary>
   /// Gets the configuration root path based on configuration type
   /// </summary>
   /// <param name="configurationType">Type of configuration storage</param>
   /// <returns>Root path for the specified configuration type</returns>
   internal static string GetRootPath(ConfigurationType configurationType) => configurationType switch
   {
      ConfigurationType.Temp => Path.GetTempPath(),
      ConfigurationType.Settings => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      ConfigurationType.Local => Directory.GetCurrentDirectory(),
      _ => Path.GetTempPath()
   };

   /// <summary>
   /// Ensures a directory exists, creating it if necessary
   /// </summary>
   /// <param name="directoryPath">Path to ensure exists</param>
   /// <exception cref="Exception">Failed to create directory</exception>
   internal static void EnsureDirectoryExists(string directoryPath)
   {
      try
      {
         if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

      }
      catch
      {
         throw new($"Failed to create directory: {directoryPath}");
      }
   }

   /// <summary>
   /// Ensures a file exists with JSON content, creating it if necessary
   /// </summary>
   /// <param name="filePath">Path to the file</param>
   /// <exception cref="Exception">Failed to create directory or read file</exception>
   internal static void EnsureJsonFileExists(string filePath)
   {
      try
      {
         var directory = Path.GetDirectoryName(filePath);

         if (!string.IsNullOrWhiteSpace(directory))
            EnsureDirectoryExists(directory);

         if (!File.Exists(filePath))
            File.WriteAllText(filePath, ConfigurationConstants.EMPTY_CONFIG);
      }
      catch
      {
         throw new($"Failed to create or access file: {filePath}");
      }
   }

   /// <summary>
   /// Ensures file has the .json extension
   /// </summary>
   /// <param name="fileName">File name to ensure has extension</param>
   /// <returns>File name with .json extension</returns>
   internal static string EnsureJsonExtension(string fileName) =>
      !fileName.EndsWith(ConfigurationConstants.CONFIG_EXTENSION, StringComparison.OrdinalIgnoreCase)
          ? fileName + ConfigurationConstants.CONFIG_EXTENSION
          : fileName;
}
