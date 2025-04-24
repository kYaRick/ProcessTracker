using Spectre.Console.Cli;

namespace ProcessTracker;

public class Program
{
   static int Main(string[] args)
   {
      var app = new CommandApp();
      return app.Run(args);
   }
}

