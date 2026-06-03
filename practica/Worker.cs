using Microsoft.Extensions.Hosting;
using practica;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Practica
{
    public class Worker : BackgroundService
    {
        private readonly ModemManager _modemManager;
        private readonly BackgroundLogger _logger;

    
        public Worker(ModemManager modemManager, BackgroundLogger logger)
        {
            _modemManager = modemManager;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
           
            stoppingToken.Register(async () =>
            {
                Console.WriteLine("Service stopping: Writing to log before shutdown");
                _modemManager.SaveDataToFile();
                await _logger.ShutdownAsync();
            });

            try
            {
              
                await _modemManager.Run();

                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal Error: {ex.Message}");
               
            }
        }
    }
}





