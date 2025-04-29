namespace ProtelScanner.WebService.Models
{
    public class Device
    {
        public string DeviceId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public bool IsAvailable { get; set; } = true;
        public string? ReservedBy { get; set; }
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
        public string? ConnectionId { get; set; }
    }
}