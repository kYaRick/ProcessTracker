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
                 .Color(Color.Green)
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

         config.AddCommand<ClearCommand>("clear")
             .WithDescription("Clear all tracked process pairs");

         config.AddCommand<MonitorCommand>("monitor")
             .WithDescription("Start monitoring mode with live updates")
             .WithExample(["monitor", "--interval", "5", "--auto-exit", "60"])
             .WithAlias("m");

         config.AddCommand<StopCommand>("stop")
             .WithDescription("Stop the background monitor process")
             .WithAlias("s");

#if DEBUG
         config.AddCommand<TestCommand>("test")
            .WithDescription("Create and monitor a test process pair for demonstration")
            .WithExample(["test"]);
#endif

         config.ValidateExamples();
      });

      return app.Run(args);
   }
}
