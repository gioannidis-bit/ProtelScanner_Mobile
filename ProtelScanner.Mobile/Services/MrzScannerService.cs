using System;
using System.Threading.Tasks;
using Camera.MAUI;
using Microsoft.Maui.Controls;
using ProtelScanner.Mobile.Models;

namespace ProtelScanner.Mobile.Services
{
    // Απλοποιημένη υλοποίηση του MrzScannerService
    // Χρησιμοποιεί απευθείας το Camera.MAUI για σάρωση 
    // και μια προσομοίωση της MRZ αναγνώρισης
    public class MrzScannerService : IMrzScannerService
    {
        private bool isScanning;
        private CameraView cameraView;

        public event EventHandler<MrzData> MrzDetected;

        public async Task StartScanningAsync(CameraView view)
        {
            if (isScanning)
                return;

            cameraView = view;
            isScanning = true;

            try
            {
                // Ρύθμιση του camera view για barcode scanning
                cameraView.BarCodeDetected += OnBarCodeDetected;

                // Έναρξη της κάμερας
                await cameraView.StartCameraAsync();
            }
            catch (Exception ex)
            {
                isScanning = false;
                throw new Exception($"Failed to start camera: {ex.Message}", ex);
            }
        }

        public void StopScanning()
        {
            if (!isScanning || cameraView == null)
                return;

            isScanning = false;
            cameraView.BarCodeDetected -= OnBarCodeDetected;
            cameraView.StopCamera();
        }

        private void OnBarCodeDetected(object sender, Camera.MAUI.ZXingHelper.BarcodeEventArgs e)
        {
            if (!isScanning || e.Result == null || e.Result.Count == 0)
                return;

            // Προσωρινά σταματάμε την αναγνώριση για να αποφύγουμε διπλές αναγνωρίσεις
            cameraView.BarCodeDetected -= OnBarCodeDetected;

            // Λαμβάνουμε το κείμενο από το barcode
            string rawMrzText = e.Result[0].Text;

            // Σε ένα πραγματικό σενάριο θα επεξεργαζόμασταν το MRZ
            // Για τους σκοπούς της επίδειξης, δημιουργούμε ένα συνθετικό MrzData

            var mrzData = new MrzData
            {
                DocumentType = "P", // Passport
                LastName = "SMITH",
                FirstName = "JOHN",
                DocumentNumber = rawMrzText.Length > 8 ? rawMrzText.Substring(0, 8) : "X12345678",
                Nationality = "GRC",
                Sex = "M",
                BirthDate = new DateTime(1990, 1, 1),
                ExpirationDate = new DateTime(2030, 1, 1),
                IssuingCountry = "GRC",
                RawMrzData = rawMrzText
            };

            // Πυροδοτούμε το event για να ενημερώσουμε τους listeners
            MrzDetected?.Invoke(this, mrzData);
        }
    }
}