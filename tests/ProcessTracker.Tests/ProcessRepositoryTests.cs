using ConfigRunner.Interfaces;
using Moq;
using ProcessTracker.Models;
using ProcessTracker.Processes;

namespace ProcessTracker.Tests.Processes;

public class ProcessRepositoryTests
{
   private readonly Mock<IConfigurationManager> _mockConfigManager;
   private readonly ProcessRepository _repository;

   public ProcessRepositoryTests()
   {
      _mockConfigManager = new Mock<IConfigurationManager>();
      _repository = new ProcessRepository(_mockConfigManager.Object);
   }

   [Fact]
   public void LoadAll_WhenConfigDoesNotExist_ReturnsEmptyList()
   {
      // Arrange
      _mockConfigManager.Setup(m => m.ReadConfiguration<List<ProcessPair>>(It.IsAny<string>()))
                        .Returns(new List<ProcessPair>());

      // Act
      var result = _repository.LoadAll();

      // Assert
      Assert.NotNull(result);
      Assert.Empty(result);
   }

   [Fact]
   public void LoadAll_WhenConfigExists_ReturnsProcessPairs()
   {
      // Arrange
      var expectedPairs = new List<ProcessPair>
        {
            new () { MainProcessId = 1, ChildProcessId = 10 },
            new () { MainProcessId = 2, ChildProcessId = 20 }
        };
      _mockConfigManager.Setup(m => m.ReadConfiguration<List<ProcessPair>>(_repository.ConfigurationFileName))
                        .Returns(expectedPairs);

      // Act
      var result = _repository.LoadAll();

      // Assert
      Assert.Equal(expectedPairs, result);
   }

   [Fact]
   public void SaveAll_CallsSaveConfiguration()
   {
      // Arrange
      var pairsToSave = new List<ProcessPair>
        {
            new ProcessPair { MainProcessId = 1, ChildProcessId = 10 }
        };

      // Act
      _repository.SaveAll(pairsToSave);

      // Assert
      _mockConfigManager.Verify(m => m.SaveConfiguration(pairsToSave, _repository.ConfigurationFileName), Times.Once);
   }

   [Fact]
   public void HasAny_WhenPairsExist_ReturnsTrue()
   {
      // Arrange
      var pairs = new List<ProcessPair> { new ProcessPair() };
      _mockConfigManager.Setup(m => m.ReadConfiguration<List<ProcessPair>>(_repository.ConfigurationFileName))
                        .Returns(pairs);

      // Act
      var result = _repository.HasAny();

      // Assert
      Assert.True(result);
   }

   [Fact]
   public void HasAny_WhenNoPairsExist_ReturnsFalse()
   {
      // Arrange
      _mockConfigManager.Setup(m => m.ReadConfiguration<List<ProcessPair>>(_repository.ConfigurationFileName))
                        .Returns(new List<ProcessPair>());

      // Act
      var result = _repository.HasAny();

      // Assert
      Assert.False(result);
   }

   [Fact]
   public void Clear_CallsRemoveAllConfigurationDirectories()
   {
      // Act
      _repository.Clear();

      // Assert
      _mockConfigManager.Verify(m => m.RemoveAllConfigurationDirectories(), Times.Once);
   }
}