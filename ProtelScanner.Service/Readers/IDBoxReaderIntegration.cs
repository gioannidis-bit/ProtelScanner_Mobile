using Microsoft.Extensions.Logging;
using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProtelScanner.Service.Readers
{
    /// <summary>
    /// Integration for IDBox MRZ readers, which primarily use serial ports
    /// </summary>
    public class IDBoxReaderIntegration : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _comPort;
        private SerialPort _serialPort;
        private bool _isConnected = false;
        private bool _disposed = false;
        private bool _continuousReading = false;
        private Task _readingTask;
        private CancellationTokenSource _cancellationTokenSource;

        // Event for when MRZ data is received
        public delegate void MrzDataReceivedHandler(string mrzData);
        public event MrzDataReceivedHandler OnMrzDataReceived;

        // Communication modes for IDBox readers
        public enum CommunicationMode
        {
            NONE = -1,
            USB_CDC = 0,
            UART_9600 = 1,
            UART_115200 = 3,
            USB_HID_CDC = 16
        }

        public IDBoxReaderIntegration(ILogger logger, string comPort)
        {
            _logger = logger;
            _comPort = comPort;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public bool Initialize()
        {
            try
            {
                _logger.LogInformation("Initializing IDBox MRZ reader on port {ComPort}", _comPort);

                // Clean up any existing connection
                Cleanup();

                // Check if the COM port exists
                if (!Array.Exists(SerialPort.GetPortNames(), port => port.Equals(_comPort, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("COM port {ComPort} not found", _comPort);
                    return false;
                }

                // Try to connect at 9600 baud first
                if (ConnectToIdBox(CommunicationMode.UART_9600))
                {
                    _logger.LogInformation("Connected to IDBox at 9600 baud");
                    _isConnected = true;
                }
                // If that fails, try 115200 baud
                else if (ConnectToIdBox(CommunicationMode.UART_115200))
                {
                    _logger.LogInformation("Connected to IDBox at 115200 baud");
                    _isConnected = true;
                }
                else
                {
                    _logger.LogError("Failed to connect to IDBox on port {ComPort}", _comPort);
                    return false;
                }

                // Start continuous reading
                EnableContinuousReading(true);

                return _isConnected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing IDBox MRZ reader");
                return false;
            }
        }

        private bool ConnectToIdBox(CommunicationMode mode)
        {
            try
            {
                // Set baudrate based on mode
                int baudrate = (mode == CommunicationMode.UART_115200) ? 115200 : 9600;

                // Create and configure the serial port
                _serialPort = new SerialPort(_comPort, baudrate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 2000,
                    WriteTimeout = 2000
                };

                // Open the port
                _serialPort.Open();
                _serialPort.DtrEnable = true;
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();

                // Test the connection by getting the version
                string version = GetVersion();
                if (string.IsNullOrEmpty(version))
                {
                    _serialPort.Close();
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to IDBox with mode {Mode}", mode);
                return false;
            }
        }

        public void EnableContinuousReading(bool enable)
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                    return;

                // Command to enable/disable continuous reading
                byte[] command = new byte[4] { 67, 0, 1, (byte)(enable ? 1 : 0) };
                _serialPort.Write(command, 0, command.Length);

                _continuousReading = enable;

                // Start background reading task if enabled
                if (enable && (_readingTask == null || _readingTask.IsCompleted))
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    _readingTask = Task.Run(() => ReadContinuously(_cancellationTokenSource.Token));
                }
                else if (!enable && _readingTask != null)
                {
                    _cancellationTokenSource.Cancel();
                }

                _logger.LogInformation("Continuous reading {Status}", enable ? "enabled" : "disabled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error {Action} continuous reading", enable ? "enabling" : "disabling");
            }
        }

        private async Task ReadContinuously(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting continuous reading task");

                while (!cancellationToken.IsCancellationRequested && _serialPort != null && _serialPort.IsOpen)
                {
                    try
                    {
                        // Check if there's data to read
                        if (_serialPort.BytesToRead > 0)
                        {
                            // Process incoming data
                            string data = ReadPacket();
                            if (!string.IsNullOrEmpty(data))
                            {
                                // Clean up the data if needed (remove trailing \r\n\r\n)
                                if (data.EndsWith("\r\n\r\n"))
                                {
                                    data = data.Substring(0, data.Length - 4);
                                }

                                // Notify about the received MRZ data
                                OnMrzDataReceived?.Invoke(data);
                            }
                        }

                        // Small delay to prevent CPU hogging
                        await Task.Delay(50, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error reading from IDBox");
                        await Task.Delay(1000, cancellationToken); // Longer delay after an error
                    }
                }

                _logger.LogInformation("Continuous reading task ended");
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
                _logger.LogInformation("Continuous reading task cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in continuous reading task");
            }
        }

        private string ReadPacket()
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                    return string.Empty;

                // Buffer for the response
                byte[] response = new byte[4096]; // Large buffer to accommodate MRZ data
                int bytesRead = 0;

                // Read header (command, length high byte, length low byte)
                if (_serialPort.Read(response, 0, 3) != 3)
                    return string.Empty;

                char cmd = (char)response[0];
                int length = response[1] * 256 + response[2];

                // Read the data if there is any
                if (length > 0)
                {
                    // Wait for all data to arrive
                    SpinWait.SpinUntil(() => _serialPort.BytesToRead >= length, 1000);

                    // Read the data
                    bytesRead = _serialPort.Read(response, 3, length);
                    if (bytesRead != length)
                        return string.Empty;
                }

                // Process the response based on the command
                switch (cmd)
                {
                    case 'I': // MRZ data
                        return Encoding.ASCII.GetString(response, 3, length).Replace("\r", "\r\n");
                    case 'V': // Version
                    case 'W': // OCR version
                    case 'R': // Serial number
                    case 'T': // Product info
                        return Encoding.ASCII.GetString(response, 3, length);
                    default:
                        return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading packet from IDBox");
                return string.Empty;
            }
        }

        // Command to manually trigger an MRZ read
        public void Inquire()
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                    return;

                byte[] command = new byte[3] { 73, 0, 0 }; // 'I' command
                _serialPort.Write(command, 0, command.Length);
                _logger.LogInformation("Inquiry command sent");

                // Read the response after a short delay
                Thread.Sleep(100);
                if (_serialPort.BytesToRead > 0)
                {
                    string response = ReadPacket();
                    if (!string.IsNullOrEmpty(response))
                    {
                        OnMrzDataReceived?.Invoke(response);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending inquiry command");
            }
        }

        public string GetVersion()
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                    return string.Empty;

                byte[] command = new byte[3] { 86, 0, 0 }; // 'V' command
                _serialPort.Write(command, 0, command.Length);

                // Small delay to allow for response
                Thread.Sleep(100);

                return ReadPacket();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting IDBox version");
                return string.Empty;
            }
        }

        public string GetSerialNumber()
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                    return string.Empty;

                byte[] command = new byte[3] { 82, 0, 0 }; // 'R' command
                _serialPort.Write(command, 0, command.Length);

                // Small delay to allow for response
                Thread.Sleep(100);

                return ReadPacket();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting IDBox serial number");
                return string.Empty;
            }
        }

        public bool IsConnected()
        {
            return _isConnected && _serialPort != null && _serialPort.IsOpen;
        }

        public void Cleanup()
        {
            try
            {
                // Stop the reading task if it's running
                if (_readingTask != null && !_readingTask.IsCompleted)
                {
                    _cancellationTokenSource.Cancel();

                    // Wait for the task to complete (with timeout)
                    try
                    {
                        _readingTask.Wait(1000);
                    }
                    catch (AggregateException)
                    {
                        // Ignore task cancellation exceptions
                    }
                }

                // Close and dispose the serial port
                if (_serialPort != null)
                {
                    if (_serialPort.IsOpen)
                    {
                        // First disable continuous reading
                        try
                        {
                            EnableContinuousReading(false);
                        }
                        catch { }

                        _serialPort.Close();
                    }
                    _serialPort.Dispose();
                    _serialPort = null;
                }

                _isConnected = false;
                _logger.LogInformation("IDBox MRZ reader cleaned up");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up IDBox MRZ reader");
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
                    Cleanup();
                    _cancellationTokenSource?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}