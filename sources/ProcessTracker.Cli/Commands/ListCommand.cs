using ProcessTracker.Cli.Services;
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
      try
      {
         var processPairs = ServiceManager.WithTemporarilySuspendedService(service =>
         {
            return service.GetAllProcessPairs();
         }, settings.QuietMode);

         if (processPairs.Count == 0)
         {
            if (!settings.QuietMode)
               AnsiConsole.MarkupLine("[blue]Info:[/] No process pairs are currently being tracked.");
            return 0;
         }

         if (!settings.QuietMode)
         {
            var table = new Table()
               .Border(TableBorder.Rounded)
               .Title("[blue]Monitored Processes[/]");

            table.AddColumn(new TableColumn("Main Process").LeftAligned());
            table.AddColumn(new TableColumn("Main ID").Centered());
            table.AddColumn(new TableColumn("Status").Centered());
            table.AddColumn(new TableColumn("Child Process").LeftAligned());
            table.AddColumn(new TableColumn("Child ID").Centered());
            table.AddColumn(new TableColumn("Added").RightAligned());

            foreach (var pair in processPairs)
            {
               var mainRunning = IsProcessRunning(pair.MainProcessId);
               var childRunning = IsProcessRunning(pair.ChildProcessId);

               var status = "";

               if (mainRunning && childRunning)
                  status = "[green]Active[/]";
               else if (mainRunning)
                  status = "[blue]Main only[/]";
               else if (childRunning)
                  status = "[yellow]Child only[/]";
               else
                  status = "[red]Inactive[/]";

               table.AddRow(
                  pair.MainProcessName,
                  pair.MainProcessId.ToString(),
                  status,
                  pair.ChildProcessName,
                  pair.ChildProcessId.ToString(),
                  pair.Time.ToString("yyyy-MM-dd HH:mm")
               );
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[blue]Total:[/] {processPairs.Count} process pairs");
         }
         else
         {
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
         var process = Process.GetProcessById(processId);
         return !process.HasExited;
      }
      catch
      {
         return false;
      }
   }
}

