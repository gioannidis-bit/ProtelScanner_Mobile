using System;
using System.Threading.Tasks;
using Camera.MAUI;
using ProtelScanner.Mobile.Models;

namespace ProtelScanner.Mobile.Services
{
    public interface IMrzScannerService
    {
        event EventHandler<MrzData> MrzDetected;

        Task StartScanningAsync(CameraView cameraView);
        void StopScanning();
    }
}