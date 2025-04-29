using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProtelScanner.Service;
using ProtelScanner.Service.Configuration;
using ProtelScanner.Service.Models;
using ProtelScanner.Service.USB;
using Serilog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProtelScanner.TestApp
{
    public class Program
    {
        private static ServiceConfig _config = null!;
        private static WebServiceClient _webServiceClient = null!;
        private static MrzReaderManager _mrzReaderManager = null!;
        private static UsbDeviceDetector _usbDetector = null!;
        private static ILogger<Program> _logger = null!;
        private static CancellationTokenSource _cts = new CancellationTokenSource();

        static async Task Main(string[] args)
        {
            // Setup configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            // Create DI container
            var serviceProvider = new ServiceCollection()
                .AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddSerilog(dispose: true);
                })
                .BuildServiceProvider();

            // Get logger
            _logger = serviceProvider.GetRequiredService<ILogger<Program>>()!;

            try
            {
                _logger.LogInformation("Starting Protel Scanner Test App");

                // Load configuration
                _config = new ServiceConfig();
                configuration.GetSection("ServiceConfig").Bind(_config);

                // Create service objects
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

                // Create WebServiceClient
                _webServiceClient = new WebServiceClient(
                    loggerFactory.CreateLogger<WebServiceClient>(),
                    _config.WebService);

                // Create MrzReaderManager
                _mrzReaderManager = new MrzReaderManager(
                    loggerFactory.CreateLogger<MrzReaderManager>(),
                    _config.MrzReader,
                    _webServiceClient);

                // Create UsbDetector
                _usbDetector = new UsbDeviceDetector(
                    loggerFactory.CreateLogger<UsbDeviceDetector>());

                // Initialize
                await _webServiceClient.InitializeAsync(_cts.Token);
                _mrzReaderManager.Initialize(_cts.Token);
                _usbDetector.StartMonitoring(_cts.Token);

                // Setup device event handlers
                _usbDetector.DeviceConnected += (deviceId, deviceName) =>
                {
                    _logger.LogInformation("USB Device Connected: {DeviceName} ({DeviceId})", deviceName, deviceId);
                };

                _usbDetector.DeviceDisconnected += (deviceId, deviceName) =>
                {
                    _logger.LogInformation("USB Device Disconnected: {DeviceName} ({DeviceId})", deviceName, deviceId);
                };

                // Display menu
                Console.WriteLine("\n======== PROTEL SCANNER TEST APP ========");
                Console.WriteLine("1 - Simulate MRZ scan (passport)");
                Console.WriteLine("2 - Simulate MRZ scan (ID card)");
                Console.WriteLine("3 - List connected USB devices");
                Console.WriteLine("4 - Check WebService connection");
                Console.WriteLine("q - Quit");
                Console.WriteLine("=========================================");

                bool running = true;
                while (running)
                {
                    Console.Write("\nEnter option: ");
                    string? option = Console.ReadLine()?.ToLower();

                    switch (option)
                    {
                        case "1":
                            SimulatePassportScan();
                            break;
                        case "2":
                            SimulateIdCardScan();
                            break;
                        case "3":
                            await ListUsbDevicesAsync();
                            break;
                        case "4":
                            await CheckWebServiceConnectionAsync();
                            break;
                        case "q":
                            running = false;
                            break;
                        default:
                            Console.WriteLine("Invalid option. Try again.");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Protel Scanner Test App");
            }
            finally
            {
                // Cleanup
                _cts.Cancel();
                _webServiceClient.Dispose();
                _mrzReaderManager.Dispose();
                _usbDetector.Dispose();

                _logger.LogInformation("Protel Scanner Test App stopped");
                Log.CloseAndFlush();
            }
        }

        private static void SimulatePassportScan()
        {
            try
            {
                Console.WriteLine("Simulating passport MRZ scan...");

                // This is a sample passport MRZ data (fictional)
                string passportMrz = "P<UTOERIKSSON<<ANNA<MARIA<<<<<<<<<<<<<<<<<<<\n" +
                                     "L898902C36UTO7408122F1204159ZE184226B<<<<<10";

                _mrzReaderManager.SimulateScan(passportMrz);
                Console.WriteLine("Passport scan simulated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating passport scan");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static void SimulateIdCardScan()
        {
            try
            {
                Console.WriteLine("Simulating ID card MRZ scan...");

                // This is a sample ID card MRZ data (fictional)
                string idCardMrz = "IDBFRAGNER<<MICHAEL<<<<<<<<<<<<<<<<<<\n" +
                                   "098657412<<<7602118M2207157BEL<<<<<94";

                _mrzReaderManager.SimulateScan(idCardMrz);
                Console.WriteLine("ID card scan simulated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating ID card scan");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static async Task ListUsbDevicesAsync()
        {
            try
            {
                Console.WriteLine("Checking connected USB devices...");
                var devices = await _usbDetector.GetConnectedUsbDevicesAsync();

                Console.WriteLine($"Found {devices.Count} USB devices:");
                foreach (var device in devices)
                {
                    string deviceId = device["DeviceID"]?.ToString() ?? "Unknown";
                    string deviceName = device["Description"]?.ToString() ?? "Unknown Device";
                    Console.WriteLine($"- {deviceName} ({deviceId})");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing USB devices");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static async Task CheckWebServiceConnectionAsync()
        {
            try
            {
                Console.WriteLine("Checking WebService connection status...");

                // For this test, we'll just try to send a dummy MRZ record
                var testData = new MrzData
                {
                    DocumentType = "P",
                    LastName = "TEST",
                    FirstName = "CONNECTION",
                    DocumentNumber = "TEST123456",
                    Nationality = "UTO",
                    Sex = "X",
                    BirthDate = DateTime.Now.AddYears(-30),
                    ExpirationDate = DateTime.Now.AddYears(10),
                    IssuingCountry = "UTO",
                    RawMrzData = "TEST CONNECTION DATA"
                };

                await _webServiceClient.SendMrzDataAsync(testData);
                Console.WriteLine("WebService connection successful.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking WebService connection");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}