using System;

namespace ProtelScanner.Service.Models
{
    public class MrzData
    {
        public string DeviceId { get; set; } = "";
        public string TerminalId { get; set; } = "";
        public string DocumentType { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string DocumentNumber { get; set; } = "";
        public string Nationality { get; set; } = "";
        public string Sex { get; set; } = "";
        public DateTime? BirthDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string IssuingCountry { get; set; } = "";
        public string RawMrzData { get; set; } = "";
    }
}