using Desko.HidApi;
using Microsoft.Extensions.Logging;
using ProtelScanner.Service.Configuration;
using System;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProtelScanner.Service.USB
{
    public class MrzReaderDevice : IDisposable
    {
        private readonly ILogger<MrzReaderDevice> _logger;
        private readonly string _deviceId;
        private readonly string _deviceName;
        private readonly MrzReaderSettings _settings;
        private SerialPort? _serialPort;
        private StringBuilder _dataBuffer = new StringBuilder();
        private bool _disposed = false;

        // Event to notify when MRZ data is received
        public delegate void MrzDataReceivedHandler(string mrzData);
        public event MrzDataReceivedHandler? OnMrzDataReceived;

        public MrzReaderDevice(
            ILogger<MrzReaderDevice> logger,
            string deviceId,
            string deviceName,
            MrzReaderSettings settings)
        {
            _logger = logger;
            _deviceId = deviceId;
            _deviceName = deviceName;
            _settings = settings;
        }

        public bool Initialize()
        {
            try
            {
                _logger.LogInformation("Initializing MRZ reader device: {DeviceName}", _deviceName);

                // If this is a serial port based reader
                if (!string.IsNullOrEmpty(_settings.ComPort))
                {
                    return InitializeSerialReader();
                }
                else
                {
                    return InitializeUsbReader();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing MRZ reader device: {DeviceName}", _deviceName);
                return false;
            }
        }

        private bool InitializeSerialReader()
        {
            try
            {
                // Configure the serial port
                _serialPort = new SerialPort(_settings.ComPort, 9600, Parity.None, 8, StopBits.One);
                _serialPort.ReadTimeout = 1000;
                _serialPort.WriteTimeout = 1000;
                _serialPort.DataReceived += SerialPort_DataReceived;

                // Open the serial port
                _serialPort.Open();
                _logger.LogInformation("Serial port {ComPort} opened for MRZ reader", _settings.ComPort);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing serial MRZ reader on port {ComPort}", _settings.ComPort);
                return false;
            }
        }

        private bool InitializeUsbReader()
        {
            try
            {
                _logger.LogInformation("Initializing USB MRZ reader");

                // Explore the HidApi assembly using reflection
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name.Contains("HidApi"));

                if (assembly == null)
                {
                    _logger.LogError("HidApi assembly not found in loaded assemblies");

                    // Try to load it manually
                    try
                    {
                        assembly = Assembly.LoadFrom("HidApi.dll");
                        _logger.LogInformation("Manually loaded HidApi.dll");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to manually load HidApi.dll");
                        return false;
                    }
                }

                // Log all types in the assembly
                _logger.LogInformation("Types in HidApi assembly:");
                foreach (var type in assembly.GetExportedTypes())
                {
                    _logger.LogInformation("- Type: {TypeName}", type.FullName);

                    // Log public methods of this type
                    foreach (var method in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                    {
                        _logger.LogInformation("  - Method: {MethodName}", method.Name);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing USB MRZ reader");
                return false;
            }
        }

        private string ConvertToMrzString(byte[] data)
        {
            // Convert byte data to string according to your reader's format
            // This will be specific to your reader's data format
            return System.Text.Encoding.ASCII.GetString(data).Trim();
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort == null)
                    return;

                // Read all available data
                string data = _serialPort.ReadExisting();

                // Append to buffer
                _dataBuffer.Append(data);

                // Check if we have a complete MRZ record
                string bufferContent = _dataBuffer.ToString();

                // Detect if the buffer contains a complete MRZ record
                // This detection logic will depend on your specific MRZ format
                // For example, looking for specific terminators or checking record length

                // Example: Simple check for passport MRZ (2 lines, 44 chars each)
                if (bufferContent.Contains("\r\n") && bufferContent.Length >= 88)
                {
                    _logger.LogDebug("Received potential MRZ data from serial port");

                    // Extract the MRZ data from the buffer
                    string mrzData = ExtractMrzFromBuffer(bufferContent);

                    if (!string.IsNullOrEmpty(mrzData))
                    {
                        // Notify listeners
                        OnMrzDataReceived?.Invoke(mrzData);
                    }

                    // Clear the buffer
                    _dataBuffer.Clear();
                }
                else if (_dataBuffer.Length > 1000)
                {
                    // Safety clear - prevent buffer overflow
                    _logger.LogWarning("MRZ buffer exceeded size limit - clearing");
                    _dataBuffer.Clear();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing serial port data");
                _dataBuffer.Clear();
            }
        }

        private string ExtractMrzFromBuffer(string buffer)
        {
            // This method should extract the MRZ data from the buffer
            // based on your specific MRZ format

            // Example implementation for 2-line passport MRZ:
            try
            {
                // Split by line breaks
                string[] lines = buffer.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                // Look for lines that match MRZ patterns
                for (int i = 0; i < lines.Length - 1; i++)
                {
                    // Check if current and next line match the pattern for passport MRZ
                    if (lines[i].Length == 44 && lines[i + 1].Length == 44 &&
                        lines[i].StartsWith("P"))
                    {
                        // Found a potential passport MRZ
                        return lines[i] + "\r\n" + lines[i + 1];
                    }
                }

                // Check for other document types too
                // ID cards, visas, etc.

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting MRZ data from buffer");
                return string.Empty;
            }
        }

        // This method would be used to handle USB device data
        // Called by your specific MRZ reader DLL callback
        public void OnUsbDataReceived(IntPtr data, int dataLength)
        {
            try
            {
                // Convert the pointer data to a string
                // Note: This would need to be adapted to your specific DLL interface
                byte[] buffer = new byte[dataLength];
                System.Runtime.InteropServices.Marshal.Copy(data, buffer, 0, dataLength);
                string mrzData = Encoding.ASCII.GetString(buffer);

                _logger.LogDebug("Received MRZ data from USB device: {DataLength} bytes", dataLength);

                // Process and notify
                if (!string.IsNullOrEmpty(mrzData))
                {
                    OnMrzDataReceived?.Invoke(mrzData);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing USB device data");
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
                    // Close the serial port if open
                    if (_serialPort != null)
                    {
                        if (_serialPort.IsOpen)
                        {
                            _serialPort.Close();
                        }
                        _serialPort.Dispose();
                        _serialPort = null;
                    }

                    // Release other resources
                    // Example: If you opened the USB device, close it here
                }

                _disposed = true;
            }
        }
    }
}