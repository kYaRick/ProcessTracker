namespace ConfigRunner.Tests;

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
