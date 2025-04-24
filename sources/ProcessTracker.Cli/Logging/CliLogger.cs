using ProcessTracker.Models;
using Spectre.Console;

namespace ProcessTracker.Cli.Logging;

/// <summary>
/// Implementation of IProcessTrackerLogger for CLI application using Spectre.Console
/// </summary>
public class CliLogger : IProcessTrackerLogger
{
   /// <summary>
   /// Logs an informational message
   /// </summary>
   public void Info(string message) =>

      AnsiConsole.MarkupLine($"[blue]INFO:[/] {EscapeMarkup(message)}");

   /// <summary>
   /// Logs a warning message
   /// </summary>
   public void Warning(string message) =>
      AnsiConsole.MarkupLine($"[yellow]WARNING:[/] {EscapeMarkup(message)}");

   /// <summary>
   /// Logs an error message
   /// </summary>
   public void Error(string message) =>
      AnsiConsole.MarkupLine($"[red]ERROR:[/] {EscapeMarkup(message)}");

   /// <summary>
   /// Escapes markup characters to prevent rendering issues
   /// </summary>
   private static string EscapeMarkup(string text) =>
      text.Replace("[", "[[").Replace("]", "]]");
}
