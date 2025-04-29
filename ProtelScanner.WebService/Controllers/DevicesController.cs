namespace ProtelScanner.WebService.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using ProtelScanner.WebService.Models;
    using ProtelScanner.WebService.Services;

    [ApiController]
    [Route("api/[controller]")]
    public class DevicesController : ControllerBase
    {
        private readonly IDeviceManagementService _deviceService;

        public DevicesController(IDeviceManagementService deviceService)
        {
            _deviceService = deviceService;
        }

        [HttpGet]
        public ActionResult<List<Device>> GetAllDevices()
        {
            return _deviceService.GetAllDevices();
        }

        [HttpGet("available")]
        public ActionResult<List<Device>> GetAvailableDevices()
        {
            return _deviceService.GetAvailableDevices();
        }

        [HttpGet("{deviceId}")]
        public ActionResult<Device> GetDevice(string deviceId)
        {
            var device = _deviceService.GetDeviceById(deviceId);
            if (device == null)
                return NotFound();

            return device;
        }

        [HttpPost("{deviceId}/reserve")]
        public ActionResult ReserveDevice(string deviceId, [FromQuery] string terminalId)
        {
            if (string.IsNullOrWhiteSpace(terminalId))
                return BadRequest("Terminal ID is required");

            if (_deviceService.ReserveDevice(deviceId, terminalId))
                return Ok();

            return BadRequest("Device is not available or does not exist");
        }

        [HttpPost("{deviceId}/release")]
        public ActionResult ReleaseDevice(string deviceId, [FromQuery] string terminalId)
        {
            if (string.IsNullOrWhiteSpace(terminalId))
                return BadRequest("Terminal ID is required");

            if (_deviceService.ReleaseDevice(deviceId, terminalId))
                return Ok();

            return BadRequest("Device is not reserved by this terminal or does not exist");
        }
    }
}
