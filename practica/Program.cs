
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using practica;


namespace Practica
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "GSM_WOL";
                })
                .ConfigureServices(services =>
                {
               
                    services.AddSingleton(new BackgroundLogger());

                  
                    services.AddSingleton<ModemManager>();

              
                    services.AddHostedService<Worker>();
                })
                .Build();

    
            await host.RunAsync();
        }
    }

};




