using ExampleApp.AppSettingsGen;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExampleApp;
public class Program
{ 
   public static async Task Main(string[] args)
   {
      Console.WriteLine("Important Note:  your IDE intellisense for appSettings properties may not work until you build the project once, and then restart your IDE.");
      /////////
      {
         Console.WriteLine("NON-DI EXAMPLE");
                  
         var appSettings = ExampleApp.AppSettingsGen.AppSettingsBinder.LoadDirect();
         Console.WriteLine(appSettings.Hello.World);
         
      }
      /////////
      {
         Console.WriteLine("DI EXAMPLE");

         HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
         builder.Services.AddSingleton<IAppSettingsBinder, AppSettingsBinder>();
         var app = builder.Build();
         var appSettings = app.Services.GetRequiredService<IAppSettingsBinder>().AppSettings;
         Console.WriteLine(appSettings.Hello.World);
      }
   }
}
