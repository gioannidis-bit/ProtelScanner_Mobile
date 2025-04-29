using System;
using Microsoft.Maui.Controls;
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