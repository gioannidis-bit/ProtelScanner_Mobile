using Microsoft.Extensions.Logging;
using System;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace ProtelScanner.Service.USB
{
    public class UsbDeviceDetector : IDisposable
    {
        private readonly ILogger<UsbDeviceDetector> _logger;
        private ManagementEventWatcher? _deviceConnectedWatcher;
        private ManagementEventWatcher? _deviceDisconnectedWatcher;
        private bool _disposed = false;

        public delegate void DeviceEventHandler(string deviceId, string deviceName);
        public event DeviceEventHandler? DeviceConnected;
        public event DeviceEventHandler? DeviceDisconnected;

        public UsbDeviceDetector(ILogger<UsbDeviceDetector> logger)
        {
            _logger = logger;
        }

        public void StartMonitoring(CancellationToken cancellationToken = default)
        {
            try
            {
                // Device connection query
                WqlEventQuery deviceConnectedQuery = new WqlEventQuery(
                    "SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'");

                // Device disconnection query
                WqlEventQuery deviceDisconnectedQuery = new WqlEventQuery(
                    "SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'");

                // Create the watchers
                _deviceConnectedWatcher = new ManagementEventWatcher(deviceConnectedQuery);
                _deviceDisconnectedWatcher = new ManagementEventWatcher(deviceDisconnectedQuery);

                // Set up event handlers
                _deviceConnectedWatcher.EventArrived += DeviceConnectedEventArrived;
                _deviceDisconnectedWatcher.EventArrived += DeviceDisconnectedEventArrived;

                // Start the watchers
                _deviceConnectedWatcher.Start();
                _deviceDisconnectedWatcher.Start();

                _logger.LogInformation("USB device monitoring started");

                // Optional: Register for cancellation
                cancellationToken.Register(() =>
                {
                    _deviceConnectedWatcher.Stop();
                    _deviceDisconnectedWatcher.Stop();
                    _logger.LogInformation("USB device monitoring stopped due to cancellation");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting USB device monitoring");
            }
        }

        private void DeviceConnectedEventArrived(object sender, EventArrivedEventArgs e)
        {
            try
            {
                ManagementBaseObject? targetInstance = e.NewEvent["TargetInstance"] as ManagementBaseObject;
                if (targetInstance != null)
                {
                    string deviceId = targetInstance["DeviceID"]?.ToString() ?? "Unknown";
                    string deviceName = targetInstance["Description"]?.ToString() ?? "Unknown USB Device";

                    _logger.LogInformation("USB device connected: {DeviceName} ({DeviceId})", deviceName, deviceId);
                    DeviceConnected?.Invoke(deviceId, deviceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing device connected event");
            }
        }

        private void DeviceDisconnectedEventArrived(object sender, EventArrivedEventArgs e)
        {
            try
            {
                ManagementBaseObject? targetInstance = e.NewEvent["TargetInstance"] as ManagementBaseObject;
                if (targetInstance != null)
                {
                    string deviceId = targetInstance["DeviceID"]?.ToString() ?? "Unknown";
                    string deviceName = targetInstance["Description"]?.ToString() ?? "Unknown USB Device";

                    _logger.LogInformation("USB device disconnected: {DeviceName} ({DeviceId})", deviceName, deviceId);
                    DeviceDisconnected?.Invoke(deviceId, deviceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing device disconnected event");
            }
        }

        // Get a list of currently connected USB devices
        public Task<ManagementObjectCollection> GetConnectedUsbDevicesAsync()
        {
            return Task.Run(() =>
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_USBHub");
                return searcher.Get();
            });
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _deviceConnectedWatcher?.Stop();
                    _deviceConnectedWatcher?.Dispose();
                    _deviceDisconnectedWatcher?.Stop();
                    _deviceDisconnectedWatcher?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}