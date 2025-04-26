using ProcessTracker.Models;
using System.Collections.Concurrent;

namespace ProcessTracker.Cli.Logging;

/// <summary>
/// Custom logger implementation that captures messages for the monitor UI
/// </summary>
public class MonitorLogger : IProcessTrackerLogger
{
   private readonly ConcurrentQueue<string> _messages;
   private readonly int _maxMessages;

   public MonitorLogger(ConcurrentQueue<string> messages, int maxMessages)
   {
      _messages = messages;
      _maxMessages = maxMessages;
   }

   public void Info(string message) => AddMessage($"[blue]INFO:[/] {EscapeMarkup(message)}");
   public void Warning(string message) => AddMessage($"[yellow]WARNING:[/] {EscapeMarkup(message)}");
   public void Error(string message) => AddMessage($"[red]ERROR:[/] {EscapeMarkup(message)}");

   private void AddMessage(string message)
   {
      _messages.Enqueue($"[grey]{DateTime.Now:HH:mm:ss}[/] {message}");

      while (_messages.Count > _maxMessages * 2)
         _messages.TryDequeue(out _);
   }

   private static string EscapeMarkup(string text) =>
      text.Replace("[", "[[").Replace("]", "]]");
}
