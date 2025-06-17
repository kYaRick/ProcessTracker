namespace ConfigRunner.Tests;

public abstract class ConfigurationManagerTestBase : IDisposable
{
   protected readonly ConfigurationManager Manager;
   protected readonly string TestRootPath;
   protected readonly string FileName = "test_config.json";

   protected ConfigurationManagerTestBase()
   {
      TestRootPath = Path.Combine(Path.GetTempPath(), $"CMT_{Guid.NewGuid()}");
      var appName = "TestApp";

      Directory.CreateDirectory(TestRootPath);
      Manager = new ConfigurationManager(TestRootPath, appName);
   }

   public void Dispose()
   {
      try
      {
         if (Directory.Exists(TestRootPath))
         {
            Directory.Delete(TestRootPath, recursive: true);
         }
      }
      catch (IOException)
      {
      }
   }
}