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

namespace ProtelScanner.Service
{
    // Add these classes to your project
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


    public class MrzReaderManager : IDisposable
    {
        private readonly ILogger<MrzReaderManager> _logger;
        private readonly MrzReaderSettings _settings;
        private readonly WebServiceClient _webServiceClient;
        private List<MrzDocDescription> _documentDescriptions;
        private string _rowsSeparator = "\r\n";
        private Timer? _pollingTimer;
        private bool _isProcessing = false;
        private bool _disposed = false;

        // Event definitions remain the same
        public delegate void MrzDataReceivedHandler(string mrzData);
        public event MrzDataReceivedHandler? OnMrzDataReceived;

        public MrzReaderManager(ILogger<MrzReaderManager> logger, MrzReaderSettings settings, WebServiceClient webServiceClient)
        {
            _logger = logger;
            _settings = settings;
            _webServiceClient = webServiceClient;
            _rowsSeparator = settings.RowSeparator == RowSeparatorType.NewLine ? "\r\n" : "\r";
            _documentDescriptions = new List<MrzDocDescription>();
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

                // Rest of initialization (timer setup, etc.) remains the same
                if (!string.IsNullOrEmpty(_settings.ComPort))
                {
                    // COM port reader setup
                    _logger.LogInformation("COM port reader not implemented yet");
                }

                // Set up the polling timer
                _pollingTimer = new Timer(_ => PollForMrzData(), null,
                    TimeSpan.Zero, TimeSpan.FromMilliseconds(_settings.DevicePollIntervalMs));

                // Set up test data handler
                OnMrzDataReceived += ProcessMrzData;

                _logger.LogInformation("MRZ Reader Manager initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing MRZ Reader Manager");
            }
        }

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

        // Helper method to get document type from the record
        private string GetDocumentType(MrzRecord record)
        {
            // Try different property names that might exist in MrzRecord
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


        private int GetIntAttribute(XmlNode node, string attrName)
        {
            var value = GetStringAttribute(node, attrName);
            if (string.IsNullOrEmpty(value)) return 0;

            return int.TryParse(value, out int result) ? result : 0;
        }


        private void PollForMrzData()
        {
            // In a real implementation, you would poll your USB device here
            // For now, this is just a placeholder
            // The actual scanning will happen through external triggers or your specific hardware interface
        }

        // This method would be called by your USB reader interface when data is received
        public void ProcessMrzData(string mrzText)
        {
            if (_isProcessing || string.IsNullOrWhiteSpace(mrzText))
                return;

            _isProcessing = true;
            try
            {
                _logger.LogDebug("Processing MRZ data: {MrzText}", mrzText);

                // Create MrzParser and parse the data
                // This assumes your MrzParser can work with our custom document descriptions
                // You may need to adapt this part based on your MrzParser's API
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

                // The rest of the method continues with handling the parsed record
                // (Convert to MrzData, handle dates, send to web service, etc.)
                // ...

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

        // This method converts our custom document descriptions to the format expected by MrzParser
        // You'll need to adapt this based on what your MrzParser actually expects
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

        // For testing - allows simulating a scan programmatically
        public void SimulateScan(string mrzData)
        {
            OnMrzDataReceived?.Invoke(mrzData);
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
                    _pollingTimer?.Dispose();
                    // Close any other resources here
                }

                _disposed = true;
            }
        }
    }
}