using ProcessTracker.Cli.Logging;
using ProcessTracker.Models;
using ProcessTracker.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;

namespace ProcessTracker.Cli.Commands;

/// <summary>
/// Command to list all tracked process pairs
/// </summary>
public class ListCommand : Command<BasicCommandSettings>
{
   public override int Execute(CommandContext context, BasicCommandSettings settings)
   {
      IProcessTrackerLogger logger = settings.QuietMode ? new QuiteLogger() : new CliLogger();

      try
      {
         using var service = new ProcessMonitorService(logger);

         if (service.IsAlreadyRunning)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[yellow]Warning:[/] Another instance of ProcessTracker is already running.");
            return 1;
         }

         var processPairs = service.GetAllProcessPairs();

         if (processPairs.Count == 0)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[blue]Info:[/] No process pairs are currently being tracked.");
            return 0;
         }

         if (!settings.QuietMode)
         {
            var table = new Table();

            table.AddColumn(new TableColumn("Main Process").Centered());
            table.AddColumn(new TableColumn("Main ID").Centered());
            table.AddColumn(new TableColumn("Child Process").Centered());
            table.AddColumn(new TableColumn("Child ID").Centered());
            table.AddColumn(new TableColumn("Status").Centered());

            foreach (var pair in processPairs)
            {
               bool mainRunning = IsProcessRunning(pair.MainProcessId);
               bool childRunning = IsProcessRunning(pair.ChildProcessId);

               string status;
               if (mainRunning && childRunning)
                  status = "[green]Active[/]";
               else if (mainRunning)
                  status = "[blue]Main only[/]";
               else if (childRunning)
                  status = "[yellow]Child only[/]";
               else
                  status = "[red]Both inactive[/]";

               table.AddRow(
                   pair.MainProcessName,
                   pair.MainProcessId.ToString(),
                   pair.ChildProcessName,
                   pair.ChildProcessId.ToString(),
                   status
               );
            }

            AnsiConsole.Write(new Rule("[yellow]Tracked Process Pairs[/]").RuleStyle("grey").Centered());
            AnsiConsole.Write(table);
         }
         else
         {
            // In quiet mode, just output process IDs
            foreach (var pair in processPairs)
            {
               Console.WriteLine($"{pair.MainProcessId},{pair.ChildProcessId}");
            }
         }

         return 0;
      }
      catch (Exception ex)
      {
         if (!settings.QuietMode)
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
         return 1;
      }
   }

   private bool IsProcessRunning(int processId)
   {
      try
      {
         Process.GetProcessById(processId);
         return true;
      }
      catch
      {
         return false;
      }
   }
}
