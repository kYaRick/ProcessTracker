namespace ConfigRunner.Tests;

public class ConfigurationManagerTests
{
   public class SaveAndReadTests : ConfigurationManagerTestBase
   {
      [Fact]
      public void SaveConfiguration_CreatesFileAndSavesCorrectData()
      {
         // Arrange
         var testData = TestDataClass.CreateWithDocuments();

         // Act
         bool success = Manager.SaveConfiguration(testData, FileName);
         var readData = Manager.ReadConfiguration<TestDataClass>(FileName);

         // Assert
         Assert.True(success);
         Assert.True(Manager.ConfigurationExists(FileName));
         Assert.NotNull(readData);
         Assert.Equal(testData.ProcessId, readData.ProcessId);
         Assert.Equal(testData.DocumentHistory.Count, readData.DocumentHistory.Count);
      }

      [Fact]
      public void ReadConfiguration_WhenFileDoesNotExist_ReturnsNull()
      {
         // Act
         var readData = Manager.ReadConfiguration<TestDataClass>(FileName);

         // Assert
         Assert.Null(readData);
      }
   }

   public class DefaultConfigurationTests : ConfigurationManagerTestBase
   {
      [Fact]
      public void SetDefaultConfiguration_CreatesFile_WhenNoneExists()
      {
         // Arrange
         var defaultData = TestDataClass.CreateDefault();

         // Act
         Manager.SetDefaultConfiguration(defaultData, FileName);
         var readData = Manager.ReadConfiguration<TestDataClass>(FileName);

         // Assert
         Assert.True(Manager.ConfigurationExists(FileName));
         Assert.NotNull(readData);
         Assert.Equal(defaultData.ProcessId, readData.ProcessId);
      }

      [Fact]
      public void SetDefaultConfiguration_DoesNotOverwriteExistingFile()
      {
         // Arrange
         var initialData = TestDataClass.CreateWithDocuments();
         var defaultData = TestDataClass.CreateDefault();
         Manager.SaveConfiguration(initialData, FileName);

         // Act
         Manager.SetDefaultConfiguration(defaultData, FileName);
         var readData = Manager.ReadConfiguration<TestDataClass>(FileName);

         // Assert
         Assert.NotNull(readData);
         Assert.Equal(initialData.ProcessId, readData.ProcessId);
      }
   }

   public class CleanupTests : ConfigurationManagerTestBase
   {
      [Fact]
      public void RemoveAllConfigurationFiles_RemovesFilesButKeepsDirectory()
      {
         // Arrange
         Manager.SaveConfiguration(TestDataClass.CreateDefault(), "file1.json");
         Manager.SaveConfiguration(TestDataClass.CreateWithDocuments(), "file2.json");
         Assert.Equal(2, Manager.GetAllConfigurationFiles().Count());

         // Act
         var result = Manager.RemoveAllConfigurationFiles();

         // Assert
         Assert.True(result);
         Assert.Empty(Manager.GetAllConfigurationFiles());
         Assert.True(Directory.Exists(Manager.ConfigurationPath));
      }

      [Fact]
      public void RemoveAllConfigurationDirectories_RemovesEntireDirectory()
      {
         // Arrange
         var path = Manager.ConfigurationPath;
         Manager.SaveConfiguration("test", "file1.json");
         Assert.True(Directory.Exists(path));

         // Act
         Manager.RemoveAllConfigurationDirectories();

         // Assert
         Assert.False(Directory.Exists(path));
      }
   }
}
