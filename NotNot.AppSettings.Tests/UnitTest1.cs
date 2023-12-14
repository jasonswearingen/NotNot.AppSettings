using System.Reflection;

namespace NotNot.SourceGenerator.AppSettings.Tests;

public class EndToEnd
{
   [Fact]
   public void AppSettingsRountripTest()
   {

      var outputFolder = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Output", "EndToEnd.AppSettingsRountripTest");
      //purge the outputFolder first
      if (Directory.Exists(outputFolder))
      {
         try
         {
            Directory.Delete(outputFolder, true);
         }catch (Exception ex)
         {
            Console.WriteLine($"Error deleting output folder:{outputFolder} with error: {ex.Message}");
         }
      }

      var gen = new AppSettingsGen();
      var startingFolder =Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"Inputs");
      var diagReports = new List<Microsoft.CodeAnalysis.Diagnostic>();
      var results = gen.GenerateSourceFiles((startingFolder, "TestInputs"), diagReports);



      foreach (var result in results)
      {
       //write each to the "Output" folder
         var outputPath = Path.Join(outputFolder, result.Key);
         Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
         File.WriteAllText(outputPath, result.Value.ToString());
      }


      Console.WriteLine("done");

   }
}