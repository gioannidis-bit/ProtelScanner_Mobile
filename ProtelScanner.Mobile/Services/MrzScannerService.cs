using System;
using System.Threading.Tasks;
using Camera.MAUI;
using Camera.MAUI.ZXingHelper;
using ZXing;
using ProtelScanner.Mobile.Models;

namespace ProtelScanner.Mobile.Services;

public class MrzScannerService : IMrzScannerService
{
    private CameraView _cameraView;

    public event EventHandler<MrzData> MrzDetected;

    public async Task StartScanningAsync(CameraView cameraView)
    {
        _cameraView = cameraView;

        var options = new BarcodeDecodeOptions
        {
            Formats = new[]
            {
                BarcodeFormat.QR_CODE,
                BarcodeFormat.CODE_39,
                BarcodeFormat.CODE_128
            },
            TryHarder = true
        };

        _cameraView.BarcodeScanningOptions = options;
        _cameraView.BarcodeDetected += OnBarcodeDetected;
        await _cameraView.StartCameraAsync();
    }

    public void StopScanning()
    {
        if (_cameraView != null)
        {
            _cameraView.BarcodeDetected -= OnBarcodeDetected;
            _ = _cameraView.StopCameraAsync();
        }
    }

    private void OnBarcodeDetected(object sender, BarcodeEventArgs e)
    {
        // Convert e.Text into your MRZ model
        var mrz = ParseMrz(e.Text);
        MrzDetected?.Invoke(this, mrz);
    }

    private MrzData ParseMrz(string raw)
    {
        // TODO: implement your MRZ parsing logic here
        return new MrzData { Raw = raw };
    }
}