namespace ProtelScanner.WebService.Services
{
    using ProtelScanner.WebService.Models;

    public interface IDeviceManagementService
    {
        List<Device> GetAllDevices();
        Device? GetDeviceById(string deviceId);
        Device? GetDeviceByConnectionId(string connectionId);
        List<Device> GetAvailableDevices();
        Device RegisterDevice(string name, string connectionId);
        bool ReserveDevice(string deviceId, string terminalId);
        bool ReleaseDevice(string deviceId, string terminalId);
        void UpdateDeviceStatus(string deviceId, bool isAvailable);
        void RemoveInactiveDevices(TimeSpan timeout);
        void UpdateDeviceLastSeen(string deviceId);
    }
}