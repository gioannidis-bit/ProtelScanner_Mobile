using Microsoft.Extensions.Logging;
using Mrz;
using Mrz.Types;
using ProtelScanner.Service.Configuration;
using ProtelScanner.Service.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Text;

namespace ProtelScanner.Service
{
    // Document description classes (unchanged)
    public class MrzDocDescription
    {
        public int Rows { get; set; }
        public int RowSize { get; set; }
        public string Country { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, MrzFieldDescription> Fields { get; set; } = new Dictionary<string, MrzFieldDescription>();
    }

    public class MrzFieldDescription
    {
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
        public int Row { get; set; }
        public int SearchFrom { get; set; }
        public string CheckDigit { get; set; } = string.Empty;
        public int StartPos => ParsePosition(Start);
        public int EndPos => ParsePosition(End);
        public int CheckDigitPos => ParsePosition(CheckDigit);

        private int ParsePosition(string value)
        {
            if (string.IsNullOrEmpty(value)) return -1;
            return int.TryParse(value, out int result) ? result : -1;
        }
    }

    // Reader type enum to support multiple reader types
    public enum ReaderType
    {
        None,
        AccessIS,
        Desko,
        IDBox
    }

    public class MrzReaderManager : IDisposable
    {
        #region AccessIS P/Invoke Declarations

        // Delegate types for AccessIS callbacks
        private delegate void msrDelegate(ref uint Parameter, [MarshalAs(UnmanagedType.LPStr)] StringBuilder data, int dataSize);
        private delegate void msrConnectionDelegate(ref uint Parameter, bool connectionStatus);

        // Keep references to prevent garbage collection
        private msrDelegate? _msrData;
        private msrConnectionDelegate? _msrDataConnection;

        // P/Invoke declarations for Access_IS_MSR.dll
        [DllImport("Access_IS_MSR.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern void initialiseMsr(bool managedCode);

        [DllImport("Access_IS_MSR.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern void msrRelease();

        [DllImport("Access_IS_MSR.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern bool enableMSR();

        [DllImport("Access_IS_MSR.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern bool disableMSR();

        [DllImport("Access_IS_MSR.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        private static extern string getDeviceName();

        [DllImport("Access_IS_MSR.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool registerMSRCallback(msrDelegate Callback, ref uint Parameter);

        [DllImport("Access_IS_MSR.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern bool registerMSRConnectionCallback(msrConnectionDelegate Callback, ref uint Parameter);

        #endregion

        #region Private Fields

        private readonly ILogger<MrzReaderManager> _logger;
        private readonly MrzReaderSettings _settings;
        private readonly WebServiceClient _webServiceClient;
        private List<MrzDocDescription> _documentDescriptions = new();
        private string _rowsSeparator = "\r\n";
        private Timer? _pollingTimer;
        private Timer? _reconnectTimer;
        private bool _isProcessing = false;
        private bool _disposed = false;
        private ReaderType _currentReaderType = ReaderType.None;
        private bool _isConnected = false;
        private object _deviceLock = new object();

        #endregion

        #region Events and Delegates

        public delegate void MrzDataReceivedHandler(string mrzData);
        public event MrzDataReceivedHandler? OnMrzDataReceived;

        #endregion

        public MrzReaderManager(ILogger<MrzReaderManager> logger, MrzReaderSettings settings, WebServiceClient webServiceClient)
        {
            _logger = logger;
            _settings = settings;
            _webServiceClient = webServiceClient;
            _rowsSeparator = settings.RowSeparator == RowSeparatorType.NewLine ? "\r\n" : "\r";
        }

        public void Initialize(CancellationToken cancellationToken = default)
        {
            try
            {
                // Load MRZ document definitions from the XML file
                string mrzDefinitionFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _settings.MrzDefinitionFile);
                if (!File.Exists(mrzDefinitionFile))
                {
                    _logger.LogError("MRZ definition file not found: {FilePath}", mrzDefinitionFile);
                    return;
                }

                // Read XML directly
                LoadDocumentDescriptions(mrzDefinitionFile);
                _logger.LogInformation("Loaded {Count} document descriptions from {FilePath}",
                    _documentDescriptions.Count, mrzDefinitionFile);

                // Set up the event handler
                OnMrzDataReceived += ProcessMrzData;

                // Determine reader type based on configuration
                _currentReaderType = DetermineReaderType();
                _logger.LogInformation("Using reader type: {ReaderType}", _currentReaderType);

                // Initialize the reader
                InitializeReader();

                // Set up auto-reconnect timer 
                _reconnectTimer = new Timer(_ => CheckAndReconnectReader(), null,
                    TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

                _logger.LogInformation("MRZ Reader Manager initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing MRZ Reader Manager");
            }
        }

        private ReaderType DetermineReaderType()
        {
            // Use settings or environment to determine which reader to use
            // For now, we'll use a simplified approach based on settings

            if (!string.IsNullOrEmpty(_settings.ReaderType))
            {
                if (_settings.ReaderType.Equals("AccessIS", StringComparison.OrdinalIgnoreCase))
                    return ReaderType.AccessIS;
                if (_settings.ReaderType.Equals("Desko", StringComparison.OrdinalIgnoreCase))
                    return ReaderType.Desko;
                if (_settings.ReaderType.Equals("IDBox", StringComparison.OrdinalIgnoreCase))
                    return ReaderType.IDBox;
            }

            // Try auto-detection by looking for available DLLs
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Access_IS_MSR.dll")))
                return ReaderType.AccessIS;

            // Additional detection logic can be added here

            // Default to AccessIS as it's the one we've tested
            return ReaderType.AccessIS;
        }

        private void InitializeReader()
        {
            try
            {
                // Clean up any existing connections first
                CleanupCurrentReader();

                switch (_currentReaderType)
                {
                    case ReaderType.AccessIS:
                        InitializeAccessISReader();
                        break;
                    case ReaderType.Desko:
                        InitializeDeskoReader();
                        break;
                    case ReaderType.IDBox:
                        InitializeIDBoxReader();
                        break;
                    default:
                        _logger.LogWarning("No reader type specified or detected");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing reader");
                _isConnected = false;
            }
        }

        private void CleanupCurrentReader()
        {
            try
            {
                lock (_deviceLock)
                {
                    switch (_currentReaderType)
                    {
                        case ReaderType.AccessIS:
                            CleanupAccessISReader();
                            break;
                        case ReaderType.Desko:
                            CleanupDeskoReader();
                            break;
                        case ReaderType.IDBox:
                            CleanupIDBoxReader();
                            break;
                    }

                    _isConnected = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up reader");
            }
        }

        private void CheckAndReconnectReader()
        {
            try
            {
                lock (_deviceLock)
                {
                    if (!_isConnected)
                    {
                        _logger.LogInformation("Reader is not connected. Attempting to reconnect...");
                        InitializeReader();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reconnecting reader");
            }
        }

        #region AccessIS Reader Implementation

        private void InitializeAccessISReader()
        {
            try
            {
                _logger.LogInformation("Initializing Access IS MSR reader");

                // Initialize the MSR reader
                initialiseMsr(true);
                _logger.LogInformation("MSR reader initialized");

                // Set up callback handlers
                uint val = 0;
                _msrData = AccessIS_MrzDataCallback;
                _msrDataConnection = AccessIS_ConnectionCallback;

                // Register callbacks
                bool callbackRegistered = registerMSRCallback(_msrData, ref val);
                _logger.LogInformation("Data callback registered: {Result}", callbackRegistered);

                bool connectionCallbackRegistered = registerMSRConnectionCallback(_msrDataConnection, ref val);
                _logger.LogInformation("Connection callback registered: {Result}", connectionCallbackRegistered);

                // Try to get device name
                try
                {
                    string deviceName = getDeviceName();
                    _logger.LogInformation("Device name: {DeviceName}", deviceName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Could not get device name: {ErrorMessage}", ex.Message);
                }

                // Enable the reader
                bool enabled = enableMSR();
                _logger.LogInformation("Reader enabled: {Result}", enabled);

                _isConnected = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Access IS MSR reader");
                _isConnected = false;
                throw;
            }
        }

        private void CleanupAccessISReader()
        {
            try
            {
                _logger.LogInformation("Cleaning up Access IS MSR reader");

                // Unregister callbacks if they were set
                if (_msrData != null || _msrDataConnection != null)
                {
                    uint val = 0;
                    registerMSRCallback(null, ref val);
                    registerMSRConnectionCallback(null, ref val);
                }

                // Release MSR resources
                msrRelease();

                // Clear callback references
                _msrData = null;
                _msrDataConnection = null;

                _logger.LogInformation("Access IS MSR reader cleaned up");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up Access IS MSR reader");
            }
        }

        private void AccessIS_MrzDataCallback(ref uint Parameter, [MarshalAs(UnmanagedType.LPStr)] StringBuilder data, int dataSize)
        {
            if (data == null || dataSize <= 0)
                return;

            try
            {
                string mrzData = data.ToString();
                _logger.LogInformation("Received MRZ data from Access IS reader ({Length} bytes)", dataSize);

                // Pass to the common handler
                OnMrzDataReceived?.Invoke(mrzData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Access IS MRZ data");
            }
        }

        private void AccessIS_ConnectionCallback(ref uint Parameter, bool connectionStatus)
        {
            try
            {
                lock (_deviceLock)
                {
                    _isConnected = connectionStatus;
                    _logger.LogInformation("Access IS connection status changed: {Status}", connectionStatus);

                    // If disconnected, we'll let the reconnect timer handle it
                    if (!connectionStatus)
                    {
                        _logger.LogWarning("Access IS reader disconnected");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Access IS connection callback");
            }
        }

        #endregion



        // Desko reader instance
        #region Desko Reader Implementation

        // Desko reader instance
        private Readers.DeskoReaderIntegration? _deskoReader;
        private Readers.DeskoReaderIntegration.MrzDataReceivedHandler? _deskoMrzHandler;

        private void InitializeDeskoReader()
        {
            try
            {
                _logger.LogInformation("Initializing Desko reader");

                // Create the Desko reader integration
                _deskoReader = new Readers.DeskoReaderIntegration(_logger);

                // Set up the MRZ data received handler
                _deskoMrzHandler = mrzData =>
                {
                    _logger.LogInformation("MRZ data received from Desko reader");
                    OnMrzDataReceived?.Invoke(mrzData);
                };
                _deskoReader.OnMrzDataReceived += _deskoMrzHandler;
            

                // Initialize the reader
                bool success = _deskoReader.Initialize();
                _isConnected = success;

                if (success)
                {
                    _logger.LogInformation("Desko reader initialized successfully");
                }
                else
                {
                    _logger.LogWarning("Failed to initialize Desko reader");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Desko reader");
                _isConnected = false;
            }
        }

        private void CleanupDeskoReader()
        {
            try
            {
                _logger.LogInformation("Cleaning up Desko reader");

                if (_deskoReader != null)
                {
                    // Clean up resources
                    if (_deskoMrzHandler != null)
                    {
                        _deskoReader.OnMrzDataReceived -= _deskoMrzHandler;
                        _deskoMrzHandler = null;
                    }
                    _deskoReader.Cleanup();
                    _deskoReader.Dispose();
                    _deskoReader = null;

                    _logger.LogInformation("Desko reader cleaned up successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up Desko reader");
            }
        }

        #endregion

        #region IDBox Reader Implementation

        // IDBox reader instance
        private Readers.IDBoxReaderIntegration? _idBoxReader;
        private Readers.IDBoxReaderIntegration.MrzDataReceivedHandler? _idBoxMrzHandler;

        private void InitializeIDBoxReader()
        {
            try
            {
                _logger.LogInformation("Initializing IDBox reader");

                // Check if COM port is specified
                if (string.IsNullOrEmpty(_settings.ComPort))
                {
                    _logger.LogError("COM port not specified for IDBox reader");
                    _isConnected = false;
                    return;
                }

                // Create the IDBox reader integration
                _idBoxReader = new Readers.IDBoxReaderIntegration(_logger, _settings.ComPort);

                // Set up the MRZ data received handler
                _idBoxMrzHandler = mrzData =>
                {
                    _logger.LogInformation("MRZ data received from IDBox reader");
                    OnMrzDataReceived?.Invoke(mrzData);
                };
                _idBoxReader.OnMrzDataReceived += _idBoxMrzHandler;

                // Initialize the reader
                bool success = _idBoxReader.Initialize();
                _isConnected = success;

                if (success)
                {
                    _logger.LogInformation("IDBox reader initialized successfully");
                }
                else
                {
                    _logger.LogWarning("Failed to initialize IDBox reader");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing IDBox reader");
                _isConnected = false;
            }
        }

        private void CleanupIDBoxReader()
        {
            try
            {
                _logger.LogInformation("Cleaning up IDBox reader");

                if (_idBoxReader != null)
                {
                    // Clean up resources
                    if (_idBoxMrzHandler != null)
                    {
                        _idBoxReader.OnMrzDataReceived -= _idBoxMrzHandler;
                        _idBoxMrzHandler = null;
                    }

                    _idBoxReader.Cleanup();
                    _idBoxReader.Dispose();
                    _idBoxReader = null;

                    _logger.LogInformation("IDBox reader cleaned up successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up IDBox reader");
            }
        }

        #endregion

        #region Document Processing

        private void LoadDocumentDescriptions(string xmlFilePath)
        {
            _documentDescriptions.Clear();

            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(xmlFilePath);

                var documentNodes = xmlDocument.SelectNodes("/DOCUMENTS/DOCUMENT");
                if (documentNodes == null)
                {
                    _logger.LogWarning("No document definitions found in XML");
                    return;
                }

                foreach (XmlNode documentNode in documentNodes)
                {
                    var docDesc = new MrzDocDescription
                    {
                        Rows = GetIntAttribute(documentNode, "ROWS"),
                        RowSize = GetIntAttribute(documentNode, "ROWSIZE"),
                        Country = GetStringAttribute(documentNode, "COUNTRY"),
                        Type = GetStringAttribute(documentNode, "TYPE")
                    };

                    // Process field definitions
                    foreach (XmlNode fieldNode in documentNode.ChildNodes)
                    {
                        if (fieldNode.NodeType != XmlNodeType.Element) continue;

                        var fieldName = fieldNode.Name;
                        var fieldDesc = new MrzFieldDescription
                        {
                            Start = GetStringAttribute(fieldNode, "START"),
                            End = GetStringAttribute(fieldNode, "END"),
                            Row = GetIntAttribute(fieldNode, "ROW"),
                            SearchFrom = GetIntAttribute(fieldNode, "SEARCHFROM"),
                            CheckDigit = GetStringAttribute(fieldNode, "CHECKDIGIT")
                        };

                        docDesc.Fields[fieldName] = fieldDesc;
                    }

                    _documentDescriptions.Add(docDesc);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading document descriptions from XML");
            }
        }

        private string GetStringAttribute(XmlNode node, string attrName)
        {
            return node.Attributes?[attrName]?.Value ?? string.Empty;
        }

        private int GetIntAttribute(XmlNode node, string attrName)
        {
            var value = GetStringAttribute(node, attrName);
            if (string.IsNullOrEmpty(value)) return 0;

            return int.TryParse(value, out int result) ? result : 0;
        }

        // This method is the common entry point for all reader types
        public void ProcessMrzData(string mrzText)
        {
            if (_isProcessing || string.IsNullOrWhiteSpace(mrzText))
                return;

            _isProcessing = true;
            try
            {
                _logger.LogDebug("Processing MRZ data: {MrzText}", mrzText);

                // Create MrzParser and parse the data
                MrzParser parser = new MrzParser(mrzText, _rowsSeparator);

                // Convert our document descriptions to the format expected by MrzParser
                var mrzDocDescriptions = ConvertToMrzParserFormat(_documentDescriptions);

                // Parse the MRZ data
                var record = parser.parse(false, mrzDocDescriptions);

                if (record == null)
                {
                    _logger.LogWarning("No matching document template for MRZ data");
                    return;
                }

                // Create MrzData model
                var mrzData = new Models.MrzData
                {
                    // Try different possible property names for document type
                    DocumentType = GetDocumentType(record),
                    LastName = record.surname,
                    FirstName = record.givenNames,
                    DocumentNumber = record.documentNumber,
                    Nationality = record.nationality == "D" ? "DEU" : record.nationality,
                    Sex = record.sex?._sex.ToString() ?? "X",
                    IssuingCountry = record.issuingCountry == "D" ? "DEU" : record.issuingCountry,
                    RawMrzData = mrzText
                };

                // Process birth date
                if (record.dateOfBirth != null)
                {
                    try
                    {
                        int birthYear = record.dateOfBirth.year;
                        if (birthYear < 100)
                        {
                            if (birthYear <= DateTime.Today.Year - 2000 && birthYear >= 0)
                                birthYear += 2000;
                            else
                                birthYear += 1900;
                        }

                        mrzData.BirthDate = new DateTime(birthYear, record.dateOfBirth.month, record.dateOfBirth.day);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse birth date");
                    }
                }

                // Process expiration date
                if (record.expirationDate != null)
                {
                    try
                    {
                        int expiryYear = record.expirationDate.year;
                        if (expiryYear < 100)
                        {
                            if (expiryYear < 50)
                                expiryYear += 2000;
                            else
                                expiryYear += 1900;
                        }

                        mrzData.ExpirationDate = new DateTime(expiryYear, record.expirationDate.month, record.expirationDate.day);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse expiration date");
                    }
                }

                // Send the data
                _webServiceClient.SendMrzDataAsync(mrzData).GetAwaiter().GetResult();
                _logger.LogInformation("Successfully processed and sent MRZ data for document: {DocumentNumber}", mrzData.DocumentNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MRZ data");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        // Helper method to get document type from the record
        private string GetDocumentType(MrzRecord record)
        {
            try
            {
                // Using reflection to find the right property
                var documentTypeProperty = record.GetType().GetProperty("DocumentType")
                    ?? record.GetType().GetProperty("documentType")
                    ?? record.GetType().GetProperty("DocumentCode")
                    ?? record.GetType().GetProperty("documentCode");

                if (documentTypeProperty != null)
                {
                    var value = documentTypeProperty.GetValue(record);
                    return value?.ToString() ?? "P";
                }

                // If no property is found, check for field instead of property
                var documentTypeField = record.GetType().GetField("DocumentType")
                    ?? record.GetType().GetField("documentType")
                    ?? record.GetType().GetField("DocumentCode")
                    ?? record.GetType().GetField("documentCode");

                if (documentTypeField != null)
                {
                    var value = documentTypeField.GetValue(record);
                    return value?.ToString() ?? "P";
                }

                // If still not found, return a default value
                return "P";
            }
            catch
            {
                // If anything goes wrong, return a default value
                return "P";
            }
        }

        // This method converts our custom document descriptions to the format expected by MrzParser
        private List<PassToProtel.XmlDocsDescr.DocDescr> ConvertToMrzParserFormat(List<MrzDocDescription> descriptions)
        {
            var result = new List<PassToProtel.XmlDocsDescr.DocDescr>();

            foreach (var desc in descriptions)
            {
                // Create a DocDescr object with the correct namespace
                var docDesc = new PassToProtel.XmlDocsDescr.DocDescr
                {
                    ROWS = desc.Rows,
                    ROWSIZE = desc.RowSize,
                    COUNTRY = desc.Country
                };

                // Set the TYPE property based on the document type
                switch (desc.Type)
                {
                    case "P":
                        docDesc.TYPE = Mrz.Types.MrzDocumentCode.DocumentCode.Passport;
                        break;
                    case "ID":
                        docDesc.TYPE = Mrz.Types.MrzDocumentCode.DocumentCode.IDCard;
                        break;
                    case "C":
                        docDesc.TYPE = Mrz.Types.MrzDocumentCode.DocumentCode.CCard;
                        break;
                    case "IDOLDGERMAN":
                        docDesc.TYPE = Mrz.Types.MrzDocumentCode.DocumentCode.IDCard_Old_German;
                        break;
                    default:
                        docDesc.TYPE = Mrz.Types.MrzDocumentCode.DocumentCode.Passport;
                        break;
                }

                // Add field definitions
                if (desc.Fields.TryGetValue("FIRSTNAME", out var firstNameField))
                {
                    docDesc.FIRSTNAME = new PassToProtel.XmlDocsDescr.BaseField
                    {
                        START = firstNameField.Start,
                        END = firstNameField.End,
                        ROW = firstNameField.Row,
                        SEARCHFROM = firstNameField.SearchFrom,
                        CHECKDIGIT = firstNameField.CheckDigit,
                        FIELD_NAME = "FIRSTNAME"
                    };
                }

                if (desc.Fields.TryGetValue("LASTNAME", out var lastNameField))
                {
                    docDesc.LASTNAME = new PassToProtel.XmlDocsDescr.BaseField
                    {
                        START = lastNameField.Start,
                        END = lastNameField.End,
                        ROW = lastNameField.Row,
                        SEARCHFROM = lastNameField.SearchFrom,
                        CHECKDIGIT = lastNameField.CheckDigit,
                        FIELD_NAME = "LASTNAME"
                    };
                }

                if (desc.Fields.TryGetValue("DOCUMENTNO", out var docNoField))
                {
                    docDesc.DOCUMENTNO = new PassToProtel.XmlDocsDescr.BaseField
                    {
                        START = docNoField.Start,
                        END = docNoField.End,
                        ROW = docNoField.Row,
                        SEARCHFROM = docNoField.SearchFrom,
                        CHECKDIGIT = docNoField.CheckDigit,
                        FIELD_NAME = "DOCUMENTNO"
                    };
                }

                if (desc.Fields.TryGetValue("SEX", out var sexField))
                {
                    docDesc.SEX = new PassToProtel.XmlDocsDescr.BaseField
                    {
                        START = sexField.Start,
                        END = sexField.End,
                        ROW = sexField.Row,
                        SEARCHFROM = sexField.SearchFrom,
                        CHECKDIGIT = sexField.CheckDigit,
                        FIELD_NAME = "SEX"
                    };
                }

                if (desc.Fields.TryGetValue("BIRTHDATE", out var birthDateField))
                {
                    docDesc.BIRTHDATE = new PassToProtel.XmlDocsDescr.BaseField
                    {
                        START = birthDateField.Start,
                        END = birthDateField.End,
                        ROW = birthDateField.Row,
                        SEARCHFROM = birthDateField.SearchFrom,
                        CHECKDIGIT = birthDateField.CheckDigit,
                        FIELD_NAME = "BIRTHDATE"
                    };
                }

                if (desc.Fields.TryGetValue("EXPIRATIONDATE", out var expirationDateField))
                {
                    docDesc.EXPIRATIONDATE = new PassToProtel.XmlDocsDescr.BaseField
                    {
                        START = expirationDateField.Start,
                        END = expirationDateField.End,
                        ROW = expirationDateField.Row,
                        SEARCHFROM = expirationDateField.SearchFrom,
                        CHECKDIGIT = expirationDateField.CheckDigit,
                        FIELD_NAME = "EXPIRATIONDATE"
                    };
                }

                if (desc.Fields.TryGetValue("ISSUINGCOUNTRY", out var issuingCountryField))
                {
                    docDesc.ISSUINGCOUNTRY = new PassToProtel.XmlDocsDescr.BaseField
                    {
                        START = issuingCountryField.Start,
                        END = issuingCountryField.End,
                        ROW = issuingCountryField.Row,
                        SEARCHFROM = issuingCountryField.SearchFrom,
                        CHECKDIGIT = issuingCountryField.CheckDigit,
                        FIELD_NAME = "ISSUINGCOUNTRY"
                    };
                }

                if (desc.Fields.TryGetValue("NATIONALITY", out var nationalityField))
                {
                    docDesc.NATIONALITY = new PassToProtel.XmlDocsDescr.BaseField
                    {
                        START = nationalityField.Start,
                        END = nationalityField.End,
                        ROW = nationalityField.Row,
                        SEARCHFROM = nationalityField.SearchFrom,
                        CHECKDIGIT = nationalityField.CheckDigit,
                        FIELD_NAME = "NATIONALITY"
                    };
                }

                result.Add(docDesc);
            }

            return result;
        }

        #endregion

        #region Utility Methods

        private void PollForMrzData()
        {
            // This method is no longer needed with our callback-based approach
            // But we keep it for compatibility
        }

        // For testing - allows simulating a scan programmatically
        public void SimulateScan(string mrzData)
        {
            OnMrzDataReceived?.Invoke(mrzData);
        }

        #endregion

        #region IDisposable Implementation

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
                    _pollingTimer?.Dispose();
                    _reconnectTimer?.Dispose();
                    CleanupCurrentReader();
                }

                _disposed = true;
            }
        }

        #endregion
    }
}