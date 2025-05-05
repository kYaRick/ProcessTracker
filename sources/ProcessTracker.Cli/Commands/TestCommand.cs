using ProcessTracker.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Command to create test processes for monitoring
/// </summary>
public class TestCommand : Command<BasicCommandSettings>
{
   public override int Execute(CommandContext context, BasicCommandSettings settings)
   {
      try
      {
         var mainProcess = new Process
         {
            StartInfo = new ProcessStartInfo
            {
               FileName = "cmd.exe",
               Arguments = "/c timeout /t 60 /nobreak > nul",
               CreateNoWindow = false
            }
         };

         mainProcess.Start();

         var childProcess = new Process
         {
            StartInfo = new ProcessStartInfo
            {
               FileName = "cmd.exe",
               Arguments = "/c timeout /t 120 /nobreak > nul",
               CreateNoWindow = false
            }
         };

         childProcess.Start();

         var (service, _) = ServiceManager.GetOrCreateService(settings.QuietMode);
         var added = service.AddProcessPair(mainProcess.Id, childProcess.Id);

         if (!settings.QuietMode)
         {
            if (added)
            {
               AnsiConsole.MarkupLine("[green]Test process pair created successfully:[/]");
               AnsiConsole.MarkupLine($"  Main process: cmd.exe (ID: {mainProcess.Id}) - will run for 60 seconds");
               AnsiConsole.MarkupLine($"  Child process: cmd.exe (ID: {childProcess.Id}) - will run for 120 seconds");
               AnsiConsole.MarkupLine("\n[blue]When the main process terminates after 60s, the child will be terminated too.[/]");
               AnsiConsole.MarkupLine("[blue]Use [green]proctrack monitor[/] to watch the test in real-time.[/]");
            }
            else
            {
               Process.GetProcessById(mainProcess.Id).Kill();
               Process.GetProcessById(childProcess.Id).Kill();

               AnsiConsole.MarkupLine("[red]Failed to add test process pair to monitoring.[/]");
            }
         }

         return added ? 0 : 1;
      }
      catch (Exception ex)
      {
         if (!settings.QuietMode)
            AnsiConsole.MarkupLine($"[red]Error creating test processes: {ex.Message}[/]");
         return 1;
      }
   }
}