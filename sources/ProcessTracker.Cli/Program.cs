using ProcessTracker.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProcessTracker.Cli;

public static class Program
{
   public static int Main(string[] args)
   {
      if (args.Length == 0)
      {
         AnsiConsole.Write(
             new FigletText("Process Tracker")
                 .Color(Color.Blue)
                 .Centered());
         AnsiConsole.WriteLine();
      }

      var app = new CommandApp();

      app.Configure(config =>
      {
         config.SetApplicationName("proctrack");

         config.AddCommand<AddCommand>("add")
             .WithDescription("Add a process pair to track")
             .WithExample(["add", "--main", "1234", "--child", "5678"]);

         config.AddCommand<RemoveCommand>("remove")
             .WithDescription("Remove a process pair from tracking")
             .WithExample(["remove", "--main", "1234", "--child", "5678"]);

         config.AddCommand<ListCommand>("list")
             .WithDescription("List all tracked process pairs");

         config.AddCommand<MonitorCommand>("monitor")
             .WithDescription("Start monitoring mode with live updates");

         config.AddCommand<StopCommand>("stop")
             .WithDescription("Stop the process tracker service");

         config.AddCommand<ServiceCommand>("service")
            .WithDescription("Run as a persistent background service");

         config.ValidateExamples();
      });

      return app.Run(args);
   }
}