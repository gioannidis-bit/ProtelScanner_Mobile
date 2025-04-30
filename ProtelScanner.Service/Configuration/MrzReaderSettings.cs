using System;

namespace ProtelScanner.Service.Configuration
{
    public enum RowSeparatorType
    {
        NewLine,
        NewLineBreak
    }

    public class MrzReaderSettings
    {
        // Path to the XML file containing MRZ document definitions
        public string MrzDefinitionFile { get; set; } = "Documents.xml";

        // Interval for polling USB devices (in milliseconds)
        public int DevicePollIntervalMs { get; set; } = 1000;

        // Enable detailed logging
        public bool EnableLogging { get; set; } = true;

        // Serial port for readers that use COM ports
        public string ComPort { get; set; } = "";

        // Row separator type for MRZ data
        public RowSeparatorType RowSeparator { get; set; } = RowSeparatorType.NewLine;

        // Type of reader to use (AccessIS, Desko, IDBox) - if empty, it will auto-detect
        public string ReaderType { get; set; } = "";

        // Auto-reconnect interval in seconds
        public int ReconnectIntervalSeconds { get; set; } = 10;

        // Maximum number of reconnection attempts (0 for unlimited)
        public int MaxReconnectAttempts { get; set; } = 0;

        // Vendor ID and Product ID for USB devices (hexadecimal) - used for specific devices
        public string VendorId { get; set; } = "";
        public string ProductId { get; set; } = "";

        // Recovery settings
        public bool AutoRecoveryEnabled { get; set; } = true;
        public int RecoveryTimeoutSeconds { get; set; } = 30;
    }
}