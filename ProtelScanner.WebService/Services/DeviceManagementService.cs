namespace ProtelScanner.WebService.Services
{
    using ProtelScanner.WebService.Models;
    using System.Collections.Concurrent;

    public class DeviceManagementService : IDeviceManagementService
    {
        private readonly ConcurrentDictionary<string, Device> _devices = new();
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

        public DeviceManagementService()
        {
            _cleanupTimer = new Timer(_ =>
                RemoveInactiveDevices(TimeSpan.FromMinutes(15)),
                null, _cleanupInterval, _cleanupInterval);
        }

        public List<Device> GetAllDevices()
        {
            return _devices.Values.ToList();
        }

        public Device? GetDeviceById(string deviceId)
        {
            _devices.TryGetValue(deviceId, out var device);
            return device;
        }

        public Device? GetDeviceByConnectionId(string connectionId)
        {
            return _devices.Values.FirstOrDefault(d => d.ConnectionId == connectionId);
        }

        public List<Device> GetAvailableDevices()
        {
            return _devices.Values.Where(d => d.IsAvailable).ToList();
        }

        public Device RegisterDevice(string name, string connectionId)
        {
            // Check if the device already exists with this connectionId
            var existingDevice = GetDeviceByConnectionId(connectionId);
            if (existingDevice != null)
            {
                existingDevice.LastSeen = DateTime.UtcNow;
                existingDevice.IsAvailable = true;
                existingDevice.Name = name;
                return existingDevice;
            }

            // Create a new device
            var newDevice = new Device
            {
                Name = name,
                ConnectionId = connectionId,
                LastSeen = DateTime.UtcNow
            };

            _devices[newDevice.DeviceId] = newDevice;
            return newDevice;
        }

        public bool ReserveDevice(string deviceId, string terminalId)
        {
            if (_devices.TryGetValue(deviceId, out var device) && device.IsAvailable)
            {
                device.IsAvailable = false;
                device.ReservedBy = terminalId;
                device.LastSeen = DateTime.UtcNow;
                return true;
            }
            return false;
        }

        public bool ReleaseDevice(string deviceId, string terminalId)
        {
            if (_devices.TryGetValue(deviceId, out var device) &&
                device.ReservedBy == terminalId)
            {
                device.IsAvailable = true;
                device.ReservedBy = null;
                device.LastSeen = DateTime.UtcNow;
                return true;
            }
            return false;
        }

        public void UpdateDeviceStatus(string deviceId, bool isAvailable)
        {
            if (_devices.TryGetValue(deviceId, out var device))
            {
                device.IsAvailable = isAvailable;
                device.LastSeen = DateTime.UtcNow;
            }
        }

        public void RemoveInactiveDevices(TimeSpan timeout)
        {
            var cutoff = DateTime.UtcNow - timeout;
            var inactiveDevices = _devices.Values
                .Where(d => d.LastSeen < cutoff)
                .Select(d => d.DeviceId)
                .ToList();

            foreach (var deviceId in inactiveDevices)
            {
                _devices.TryRemove(deviceId, out _);
            }
        }

        public void UpdateDeviceLastSeen(string deviceId)
        {
            if (_devices.TryGetValue(deviceId, out var device))
            {
                device.LastSeen = DateTime.UtcNow;
            }
        }
    }
}
