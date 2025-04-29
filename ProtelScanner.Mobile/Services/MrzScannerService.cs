using System;
using System.Threading.Tasks;
using Camera.MAUI;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using ProtelScanner.Mobile.Models;

namespace ProtelScanner.Mobile.Services
{
    // Ενημερωμένη υλοποίηση του MrzScannerService
    // Χρησιμοποιεί το Camera.MAUI για σάρωση και
    // παρέχει έναν απλό μηχανισμό ανίχνευσης MRZ
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
                // Ρύθμιση του camera view
                cameraView.BarCodeOptions = new Camera.MAUI.ZXingHelper.BarcodeDecodeOptions
                {
                    TryHarder = true,
                    PossibleFormats = new List<ZXing.BarcodeFormat> { 
                        ZXing.BarcodeFormat.QR_CODE, 
                        ZXing.BarcodeFormat.CODE_39, 
                        ZXing.BarcodeFormat.CODE_128 
                    }
                };
                
                // Ρύθμιση του barcode event
                cameraView.BarcodeDetected += OnBarcodeDetected;

                // Έναρξη της κάμερας
                var result = await cameraView.StartCameraAsync();
                if (!result)
                {
                    throw new Exception("Δεν ήταν δυνατή η έναρξη της κάμερας");
                }
            }
            catch (Exception ex)
            {
                isScanning = false;
                throw new Exception($"Σφάλμα κατά την έναρξη της κάμερας: {ex.Message}", ex);
            }
        }

        public void StopScanning()
        {
            if (!isScanning || cameraView == null)
                return;

            isScanning = false;
            cameraView.BarcodeDetected -= OnBarcodeDetected;
            cameraView.StopCameraAsync();
        }

        private void OnBarcodeDetected(object sender, Camera.MAUI.ZXingHelper.BarcodeEventArgs e)
        {
            if (!isScanning || e == null || e.Result == null)
                return;

            string barcodeText = e.Result.Text;
            if (string.IsNullOrEmpty(barcodeText))
                return;

            // Προσωρινά σταματάμε την αναγνώριση για να αποφύγουμε διπλές αναγνωρίσεις
            cameraView.BarcodeDetected -= OnBarcodeDetected;

            // Αναλύουμε το κείμενο για να εξάγουμε τα στοιχεία MRZ
            // Σε πραγματική εφαρμογή θα είχαμε πιο εξελιγμένο parser
            var mrzData = ParseMrzText(barcodeText);

            // Πυροδοτούμε το event για να ενημερώσουμε τους listeners
            MainThread.BeginInvokeOnMainThread(() => {
                MrzDetected?.Invoke(this, mrzData);
            });
        }

        private MrzData ParseMrzText(string rawText)
        {
            // Απλοποιημένο parsing για demo
            // Σε πραγματική εφαρμογή θα είχαμε πιο πολύπλοκο parsing

            // Δημιουργούμε ένα δοκιμαστικό MrzData
            var mrzData = new MrzData
            {
                DocumentType = "P", // Passport
                LastName = "SMITH",
                FirstName = "JOHN",
                DocumentNumber = rawText.Length > 8 ? rawText.Substring(0, 8) : "X12345678",
                Nationality = "GRC",
                Sex = "M",
                BirthDate = DateTime.Now.AddYears(-30),
                ExpirationDate = DateTime.Now.AddYears(5),
                IssuingCountry = "GRC",
                RawMrzData = rawText
            };

            return mrzData;
        }
    }
}