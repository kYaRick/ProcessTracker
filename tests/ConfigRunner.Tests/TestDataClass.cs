namespace ConfigRunner.Tests;

public class TestDataClass
{
   public int ProcessId { get; set; }
   public Dictionary<string, string> DocumentHistory { get; set; } = new();

   public static TestDataClass CreateDefault() => new() { ProcessId = 4242 };

   public static TestDataClass CreateWithDocuments() => new()
   {
      ProcessId = 7242,
      DocumentHistory = new Dictionary<string, string>
        {
            { "TestDoc1", "Path1" },
            { "TestDoc2", "Path2" }
        }
   };
}
