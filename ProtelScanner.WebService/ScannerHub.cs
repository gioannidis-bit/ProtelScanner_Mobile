using Microsoft.AspNetCore.Mvc;

namespace ProtelScanner.WebService
{
    using Microsoft.AspNetCore.SignalR;
    using ProtelScanner.WebService.Models;
    using ProtelScanner.WebService.Services;
    using System.Threading.Tasks;

    public class ScannerHub : Hub
    {
        private readonly IDeviceManagementService _deviceService;

        public ScannerHub(IDeviceManagementService deviceService)
        {
            _deviceService = deviceService;
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var device = _deviceService.GetDeviceByConnectionId(Context.ConnectionId);
            if (device != null)
            {
                device.IsAvailable = true;
                if (!string.IsNullOrEmpty(device.ReservedBy))
                {
                    await Clients.Group(device.ReservedBy).SendAsync("DeviceDisconnected", device.DeviceId);
                    device.ReservedBy = null;
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task RegisterDevice(string deviceName)
        {
            var device = _deviceService.RegisterDevice(deviceName, Context.ConnectionId);
            await Clients.Caller.SendAsync("DeviceRegistered", device.DeviceId);
        }

        public async Task JoinTerminalGroup(string terminalId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, terminalId);
        }

        public async Task LeaveTerminalGroup(string terminalId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, terminalId);
        }

        public async Task SendMrzData(MrzData mrzData)
        {
            if (string.IsNullOrEmpty(mrzData.TerminalId))
                return;

            var device = _deviceService.GetDeviceById(mrzData.DeviceId);
            if (device != null && device.ReservedBy == mrzData.TerminalId)
            {
                await Clients.Group(mrzData.TerminalId).SendAsync("ReceiveMrzData", mrzData);
            }
        }

        public async Task WakeupDevice(string deviceId, string terminalId)
        {
            var device = _deviceService.GetDeviceById(deviceId);
            if (device != null && device.ConnectionId != null && device.ReservedBy == terminalId)
            {
                await Clients.Client(device.ConnectionId).SendAsync("Wakeup");
            }
        }
    }
}
