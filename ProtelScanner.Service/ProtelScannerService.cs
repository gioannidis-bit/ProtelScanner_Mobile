using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProtelScanner.Service.Configuration;
using ProtelScanner.Service.USB;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Management;

namespace ProtelScanner.Service
{
    public class ProtelScannerService : BackgroundService
    {
        private readonly ILogger<ProtelScannerService> _logger;
        private readonly ServiceConfig _config;
        private readonly WebServiceClient _webServiceClient;
        private readonly MrzReaderManager _mrzReaderManager;
        private readonly UsbDeviceDetector _usbDeviceDetector;

        public ProtelScannerService(
      ILogger<ProtelScannerService> logger,
      ServiceConfig config,
      WebServiceClient webServiceClient,
      MrzReaderManager mrzReaderManager,
      UsbDeviceDetector usbDeviceDetector)
        {
            _logger = logger;
            _config = config;
            _webServiceClient = webServiceClient;
            _mrzReaderManager = mrzReaderManager;
            _usbDeviceDetector = usbDeviceDetector;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Protel Scanner Service is starting");

                // Set up USB device detection events
                _usbDeviceDetector.DeviceConnected += (deviceId, deviceName) =>
                {
                    _logger.LogInformation("Checking if connected device is an MRZ reader: {DeviceName}", deviceName);
                    // Here you would check if the device is an MRZ reader and initialize it
                    // For now, we'll just log it
                };

                _usbDeviceDetector.DeviceDisconnected += (deviceId, deviceName) =>
                {
                    _logger.LogInformation("MRZ reader may have been disconnected: {DeviceName}", deviceName);
                    // Here you would handle the device disconnection
                };

                // Start USB device monitoring
                _usbDeviceDetector.StartMonitoring(stoppingToken);

                // Initialize the WebService client
                await _webServiceClient.InitializeAsync(stoppingToken);

                // Initialize the MRZ reader manager
                _mrzReaderManager.Initialize(stoppingToken);

                // Log connected USB devices at startup
                var devices = await _usbDeviceDetector.GetConnectedUsbDevicesAsync();
                _logger.LogInformation("Found {Count} USB devices connected at startup", devices.Count);

                foreach (ManagementObject device in devices)
                {
                    string? deviceId = device["DeviceID"]?.ToString();
                    string? deviceName = device["Description"]?.ToString();

                    _logger.LogInformation("USB Device: {DeviceName} ({DeviceId})",
                        deviceName ?? "Unknown", deviceId ?? "Unknown");

                    // Check if this device is in our configured list
                    // This is where you would check if a device is an MRZ reader
                    // and configure it accordingly
                }

                // Service is now running - keep it alive until stopping is requested
                _logger.LogInformation("Protel Scanner Service is now running");

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                _logger.LogInformation("Service shutdown requested");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Protel Scanner Service");
            }
            finally
            {
                _logger.LogInformation("Protel Scanner Service is stopping");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Protel Scanner Service");
            await base.StopAsync(cancellationToken);
        }
    }
}