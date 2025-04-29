using System;
using System.Collections.Generic;

namespace ProtelScanner.Service.Configuration
{
    public class ServiceConfig
    {
        public WebServiceSettings WebService { get; set; } = new WebServiceSettings();
        public MrzReaderSettings MrzReader { get; set; } = new MrzReaderSettings();
        public List<DeviceConfig> Devices { get; set; } = new List<DeviceConfig>();
    }

    public class WebServiceSettings
    {
        public string Url { get; set; } = "https://localhost:7067/scannerhub";
        public string DeviceName { get; set; } = "USB MRZ Reader";
        public string TerminalId { get; set; } = "DEFAULT";
        public int ReconnectIntervalSeconds { get; set; } = 5;
    }

    public class MrzReaderSettings
    {
        public string MrzDefinitionFile { get; set; } = "Documents.xml";
        public int DevicePollIntervalMs { get; set; } = 1000;
        public bool EnableLogging { get; set; } = true;
        public string ComPort { get; set; } = ""; // For serial MRZ readers
        public RowSeparatorType RowSeparator { get; set; } = RowSeparatorType.NewLine;
    }

    public class DeviceConfig
    {
        public string Name { get; set; } = "";
        public string DeviceId { get; set; } = ""; // Only used if your service needs to identify specific devices
        public bool Enabled { get; set; } = true;
    }

    public enum RowSeparatorType
    {
        NewLine,
        NewLineBreak
    }
}