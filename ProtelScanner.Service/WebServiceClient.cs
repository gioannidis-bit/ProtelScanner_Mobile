using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using ProtelScanner.Service.Configuration;
using ProtelScanner.Service.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtelScanner.Service
{
    public class WebServiceClient : IDisposable
    {
        private readonly ILogger<WebServiceClient> _logger;
        private readonly WebServiceSettings _settings;
        private HubConnection _hubConnection;
        private string _deviceId = "";
        private bool _isConnected = false;
        private Timer? _reconnectTimer;
        private bool _disposed = false;

        public WebServiceClient(ILogger<WebServiceClient> logger, WebServiceSettings settings)
        {
            _logger = logger;
            _settings = settings;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await ConnectAsync();

            // Set up reconnect timer
            _reconnectTimer = new Timer(async _ =>
            {
                if (!_isConnected && !cancellationToken.IsCancellationRequested)
                {
                    await ConnectAsync();
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(_settings.ReconnectIntervalSeconds));
        }

        private async Task ConnectAsync()
        {
            try
            {
                if (_hubConnection != null)
                {
                    await _hubConnection.DisposeAsync();
                }

                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(_settings.Url)
                    .WithAutomaticReconnect(new[] {
                        TimeSpan.FromSeconds(0),
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(10)
                    })
                    .Build();

                // Set up connection event handlers
                _hubConnection.Closed += async (error) =>
                {
                    _isConnected = false;
                    _logger.LogWarning("Connection closed with error: {Error}", error?.Message);
                };

                _hubConnection.Reconnecting += error =>
                {
                    _isConnected = false;
                    _logger.LogInformation("Attempting to reconnect: {Error}", error?.Message);
                    return Task.CompletedTask;
                };

                _hubConnection.Reconnected += async (connectionId) =>
                {
                    _isConnected = true;
                    _logger.LogInformation("Reconnected with connection ID: {ConnectionId}", connectionId);
                    await RegisterDeviceAsync();
                    await JoinTerminalGroupAsync();
                };

                // Listen for "Wakeup" notifications from the server
                _hubConnection.On("Wakeup", () =>
                {
                    _logger.LogInformation("Received wakeup signal from server");
                    // You can add code here to trigger a scan if needed
                });

                // Listen for device registration confirmation
                _hubConnection.On<string>("DeviceRegistered", (deviceId) =>
                {
                    _deviceId = deviceId;
                    _logger.LogInformation("Device registered with ID: {DeviceId}", deviceId);
                });

                // Start the connection
                await _hubConnection.StartAsync();
                _isConnected = true;
                _logger.LogInformation("Connected to WebService");

                // Register the device and join the terminal group
                await RegisterDeviceAsync();
                await JoinTerminalGroupAsync();
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _logger.LogError(ex, "Error connecting to WebService");
            }
        }

        private async Task RegisterDeviceAsync()
        {
            if (_isConnected)
            {
                try
                {
                    await _hubConnection.InvokeAsync("RegisterDevice", _settings.DeviceName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error registering device");
                }
            }
        }

        private async Task JoinTerminalGroupAsync()
        {
            if (_isConnected)
            {
                try
                {
                    await _hubConnection.InvokeAsync("JoinTerminalGroup", _settings.TerminalId);
                    _logger.LogInformation("Joined terminal group: {TerminalId}", _settings.TerminalId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error joining terminal group");
                }
            }
        }

        public async Task SendMrzDataAsync(MrzData mrzData)
        {
            if (!_isConnected || string.IsNullOrEmpty(_deviceId))
            {
                _logger.LogWarning("Cannot send MRZ data - not connected or device not registered");
                return;
            }

            try
            {
                // Set the device ID and terminal ID
                mrzData.DeviceId = _deviceId;
                mrzData.TerminalId = _settings.TerminalId;

                // Send the MRZ data to the server
                await _hubConnection.InvokeAsync("SendMrzData", mrzData);
                _logger.LogInformation("Sent MRZ data for document: {DocumentNumber}", mrzData.DocumentNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending MRZ data");
            }
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
                    _reconnectTimer?.Dispose();
                    _hubConnection?.DisposeAsync().GetAwaiter().GetResult();
                }

                _disposed = true;
            }
        }
    }
}