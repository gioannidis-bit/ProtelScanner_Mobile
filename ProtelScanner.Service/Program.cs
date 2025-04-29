using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProtelScanner.Service;
using ProtelScanner.Service.Configuration;
using ProtelScanner.Service.USB;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ProtelScanner.Service
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Set up the configuration from appsettings.json
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            var configuration = configurationBuilder.Build();

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .CreateLogger();

            try
            {
                Log.Information("Starting Protel Scanner Service");

                // Create the host with the Windows service support
                await CreateHostBuilder(args, configuration).Build().RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Protel Scanner Service terminated unexpectedly");
            }
            finally
            {
                Log.Information("Protel Scanner Service stopped");
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args, IConfiguration configuration) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "ProtelScannerService";
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Configuration
                    var serviceConfig = new ServiceConfig();
                    configuration.GetSection("ServiceConfig").Bind(serviceConfig);
                    services.AddSingleton(serviceConfig);

                    // Register individual settings objects
                    services.AddSingleton(serviceConfig.WebService);
                    services.AddSingleton(serviceConfig.MrzReader);

                    // Register service dependencies
                    services.AddSingleton<WebServiceClient>();
                    services.AddSingleton<MrzReaderManager>();
                    services.AddSingleton<UsbDeviceDetector>();

                    // Add the Windows service
                    services.AddHostedService<ProtelScannerService>();
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddSerilog(dispose: true);
                });
    }
}