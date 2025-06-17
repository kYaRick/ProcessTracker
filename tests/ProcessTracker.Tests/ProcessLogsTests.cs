using ProcessTracker.Processes;
using System.Diagnostics;

public class ProcessLogsTests : IDisposable
{
   private readonly StringWriter _stringWriter;
   private readonly TextWriterTraceListener _listener;
   private readonly ProcessLogs _logger;

   public ProcessLogsTests()
   {
      _stringWriter = new StringWriter();
      _listener = new TextWriterTraceListener(_stringWriter);
      _logger = new ProcessLogs();

      Trace.Listeners.Add(_listener);
   }

   [Fact]
   public void Info_LogsCorrectMessageToDebug()
   {
      // Arrange
      var message = "Test info message";
      var expectedLog = $"INFO: {message}";

      // Act
      _logger.Info(message);
      Debug.Flush();

      // Assert
      var actualLog = _stringWriter.ToString();
      Assert.Contains(expectedLog, actualLog);
   }

   [Fact]
   public void Warning_LogsCorrectMessageToDebug()
   {
      // Arrange
      var message = "Test warning message";
      var expectedLog = $"WARNING: {message}";

      // Act
      _logger.Warning(message);
      Debug.Flush();

      // Assert
      var actualLog = _stringWriter.ToString();
      Assert.Contains(expectedLog, actualLog);
   }

   [Fact]
   public void Error_LogsCorrectMessageToDebug()
   {
      // Arrange
      var message = "Test error message";
      var expectedLog = $"ERROR: {message}";

      // Act
      _logger.Error(message);
      Debug.Flush();

      // Assert
      var actualLog = _stringWriter.ToString();
      Assert.Contains(expectedLog, actualLog);
   }

   public void Dispose()
   {
      Trace.Listeners.Remove(_listener);
      _listener.Close();
      _stringWriter.Dispose();
   }
}