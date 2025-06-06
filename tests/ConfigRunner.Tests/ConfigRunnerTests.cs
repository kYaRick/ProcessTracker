using ConfigRunner;
using ConfigRunner.Constants;
using ConfigRunner.Utilities;
using System.Text.Json;

namespace Gpak.ConfigurationRunner.Tests;

public class TestDataClass
{
   public int ProcessId { get; set; } = 0;
   public Dictionary<string, string> DocumentHistory { get; set; } = new();
   public static string GetFileName => $"other{ConfigurationConstants.CONFIG_EXTENSION}";

   public static TestDataClass CreateDefault() =>
       new() { ProcessId = 4242 };

   public static TestDataClass CreateWithDocuments() =>
       new()
       {
          ProcessId = 7242,
          DocumentHistory = new()
          {
               { "TestDoc", "TestPath" },
               { "TestDoc1", "TestPath2" },
               { "TestDoc3", "TestPath3" }
          }
       };
}

public class ConfigurationManagerTests
{
   private readonly string _applicationName = nameof(ConfigurationManagerTests);
   private readonly string _fileName = $"test.json";

   [Fact]
   public void Constructor_WithNonExistentPath_ShouldThrowArgumentException()
   {
      // Arrange
      var nonExistentPath = Path.Combine(Path.GetTempPath(), nameof(ConfigurationManagerTests));

      // Act & Assert
      Assert.False(Directory.Exists(nonExistentPath));
      Assert.Throws<ArgumentException>(() =>
          new ConfigurationManager(nonExistentPath, string.Empty));
   }

   [Fact]
   public void Constructor_WithCustomPath_ShouldCreatedAndDeleteConfig()
   {
      // Arrange
      var subFolderName = "test_dir";
      var targetedPath = Path.Combine(Directory.GetCurrentDirectory(), subFolderName);
      var expectedExistDir = Path.Combine(targetedPath, _applicationName);

      // Act
      Directory.CreateDirectory(targetedPath);
      var manager = new ConfigurationManager(targetedPath, _applicationName);

      // Assert
      Assert.Equal(expectedExistDir, manager.ConfigurationDirectory);
      Assert.True(Directory.Exists(expectedExistDir));

      // Cleanup
      CleanupTestDirectory(manager);
   }

   [Theory]
   [InlineData(ConfigurationType.Temp)]
   [InlineData(ConfigurationType.Settings)]
   [InlineData(ConfigurationType.Local)]
   public void CreateAndSaveConfiguration_ShouldSucceed(ConfigurationType type)
   {
      // Arrange
      var manager = new ConfigurationManager(type, _applicationName);
      var expectedPath = manager.ConfigurationDirectory;

      // Act & Assert
      Assert.True(Directory.Exists(expectedPath));
      Assert.True(type switch
      {
         ConfigurationType.Temp => expectedPath.Contains("Local") && expectedPath.Contains("Temp"),
         ConfigurationType.Settings => expectedPath.Contains("AppData"),
         _ => true
      });

      // Setup test data
      var testData = TestDataClass.CreateWithDocuments();

      // Save configuration
      var success = manager.SaveConfiguration(testData, _fileName);
      Assert.True(success);

      var filePath = Path.Combine(expectedPath, _fileName);
      Assert.True(File.Exists(filePath));

      // Read and compare
      var storedJson = File.ReadAllText(filePath);
      var testDataJson = JsonSerializer.Serialize(testData,
          JsonSerializerUtilities.DefaultOptions);

      // Deserialize both to normalize any formatting differences
      var storedObject = JsonSerializer.Deserialize<TestDataClass>(storedJson,
          JsonSerializerUtilities.DefaultOptions);
      var testObject = JsonSerializer.Deserialize<TestDataClass>(testDataJson,
          JsonSerializerUtilities.DefaultOptions);

      Assert.Equal(testObject?.ProcessId, storedObject?.ProcessId);
      Assert.Equal(testObject?.DocumentHistory.Count, storedObject?.DocumentHistory.Count);

      // Cleanup
      CleanupTestDirectory(manager);
   }

   [Fact]
   public void ReadConfiguration_ShouldReturnCorrectData()
   {
      // Arrange
      var manager = new ConfigurationManager(ConfigurationType.Settings, _applicationName);
      var testData = TestDataClass.CreateWithDocuments();

      // Act
      manager.SaveConfiguration(testData, _fileName);
      var readData = manager.ReadConfiguration<TestDataClass>(_fileName);

      // Assert
      Assert.NotNull(readData);
      Assert.Equal(testData.ProcessId, readData.ProcessId);
      Assert.Equal(testData.DocumentHistory.Count, readData.DocumentHistory.Count);
      Assert.Contains("Path", readData.DocumentHistory.First().Value);

      // Cleanup
      CleanupTestDirectory(manager);
   }

   [Fact]
   public void SetDefaultConfiguration_ShouldNotOverwriteExistingConfiguration()
   {
      // Arrange
      var manager = new ConfigurationManager(ConfigurationType.Settings, _applicationName);
      var testData = TestDataClass.CreateWithDocuments();
      var defaultData = TestDataClass.CreateDefault();

      // Act
      manager.SaveConfiguration(testData, _fileName);
      manager.SetDefaultConfiguration(defaultData, _fileName);
      var readData = manager.ReadConfiguration<TestDataClass>(_fileName);

      // Assert
      Assert.NotNull(readData);
      Assert.Equal(testData.ProcessId, readData.ProcessId);
      Assert.NotEqual(defaultData.ProcessId, readData.ProcessId);

      // Cleanup
      CleanupTestDirectory(manager);
   }

   [Fact]
   public void ClearAllConfigurations_ShouldRemoveAllFiles()
   {
      // Arrange
      var manager = new ConfigurationManager(ConfigurationType.Settings, _applicationName);
      var testData = TestDataClass.CreateDefault();
      var testFileName = TestDataClass.GetFileName;

      // Act
      manager.SaveConfiguration(testData, _fileName);
      manager.SaveConfiguration(testData, testFileName);

      var result = manager.RemoveAllConfigurationFiles();

      // Assert
      Assert.True(result);
      Assert.False(manager.ConfigurationExists(_fileName));
      Assert.False(manager.ConfigurationExists(testFileName));
      Assert.Empty(manager.GetAllConfigurationFiles());

      // Cleanup
      CleanupTestDirectory(manager);
   }

   [Fact]
   public void SerializedJson_ShouldMatchFileJson_WhenReadDirectly()
   {
      // Arrange
      var manager = new ConfigurationManager(ConfigurationType.Settings, _applicationName);
      var testData = TestDataClass.CreateWithDocuments();
      var testFileName = TestDataClass.GetFileName;

      try
      {
         // Act
         var directlySerializedJson = JsonSerializer.Serialize(
             testData, JsonSerializerUtilities.DefaultOptions);

         manager.SaveConfiguration(testData, testFileName);

         var filePath = Path.Combine(
             manager.ConfigurationDirectory, testFileName);
         var fileJson = File.ReadAllText(filePath);

         var directJsonObj = JsonDocument.Parse(directlySerializedJson).RootElement;
         var fileJsonObj = JsonDocument.Parse(fileJson).RootElement;

         var normalizedDirectJson = JsonSerializer.Serialize(directJsonObj);
         var normalizedFileJson = JsonSerializer.Serialize(fileJsonObj);

         // Assert
         Assert.Equal(normalizedDirectJson, normalizedFileJson);
      }
      finally
      {
         CleanupTestDirectory(manager);
      }
   }

   [Theory]
   [InlineData(ConfigurationType.Temp)]
   [InlineData(ConfigurationType.Settings)]
   [InlineData(ConfigurationType.Local)]
   public void RemoveFullConfiguration_ShouldRemoveDirectory(ConfigurationType type)
   {
      var manager = new ConfigurationManager(type, _applicationName);
      var expectedPath = manager.ConfigurationDirectory;
      
      Assert.True(Directory.Exists(expectedPath));

      var countOfFile = new Random().Next(20);

      for (var i = 0; i < countOfFile; i++)
      {
         var filePath = Path.Combine(expectedPath, $"TestFie{i}.json");
         manager.SaveConfiguration($"{i}", filePath);  
      }
      
      Assert.Equal(countOfFile, manager.GetAllConfigurationFiles().Count());
      
      manager.RemoveAllConfigurationDirectories();
      
      Assert.False(Directory.Exists(expectedPath));
   }
   
   
   
   private void CleanupTestDirectory(ConfigurationManager manager)
   {
      manager.RemoveAllConfigurationFiles();
      var directory = manager.ConfigurationDirectory;

      try
      {
         if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);

         directory = Path.GetDirectoryName(directory);

         if ((!Directory.GetFiles(directory!)?.Any() ?? false) &&
            (!Directory.GetDirectories(directory!)?.Any() ?? false))
            Directory.Delete(directory!, recursive: true);

      }
      catch { }
   }
}
