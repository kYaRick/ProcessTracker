namespace ConfigRunner.Tests;

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
