namespace ConfigRunner.Tests;

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
