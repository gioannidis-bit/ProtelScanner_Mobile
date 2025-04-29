using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Camera.MAUI;
using Camera.MAUI.ZXingHelper;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Maui.Controls;
using ProtelScanner.Mobile.Models;

namespace ProtelScanner.Mobile
{
    [QueryProperty(nameof(TerminalId), "TerminalId")]
    public partial class ScannerPage : ContentPage
    {
        private HubConnection hubConnection;
        private bool isScanning = false;

        public string TerminalId { get; set; }

        public ScannerPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Ανάκτηση του hub connection
            if (App.Current.Properties.ContainsKey("HubConnection"))
            {
                hubConnection = App.Current.Properties["HubConnection"] as HubConnection;
            }

            if (hubConnection == null || hubConnection.State != HubConnectionState.Connected)
            {
                await DisplayAlert("Error", "Connection to server lost", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // Έλεγχος δικαιωμάτων κάμερας
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("Permission Required", "Camera permission is required to scan documents", "OK");
                    await Shell.Current.GoToAsync("..");
                    return;
                }
            }

            // Εκκίνηση σάρωσης
            await StartScanningAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Διακοπή σάρωσης όταν κλείνει η σελίδα
            StopScanning();
        }

        private async Task StartScanningAsync()
        {
            try
            {
                isScanning = true;
                statusLabel.Text = "Scanning in progress...";

                // Ρύθμιση της κάμερας για αναγνώριση barcodes (θα χρησιμοποιηθεί για MRZ)
                cameraView.BarCodeOptions = new BarcodeDecodeOptions
                {
                    TryHarder = true,
                    PossibleFormats = { ZXing.BarcodeFormat.All_1D }
                };

                cameraView.BarCodeDetected += CameraView_BarCodeDetected;
                cameraView.CameraLocation = CameraLocation.Back;

                // Εκκίνηση της κάμερας
                await cameraView.StartCameraAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Scanner Error", $"Failed to start scanner: {ex.Message}", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }

        private void StopScanning()
        {
            if (!isScanning)
                return;

            isScanning = false;

            cameraView.BarCodeDetected -= CameraView_BarCodeDetected;
            cameraView.StopCamera();
        }

        private void CameraView_BarCodeDetected(object sender, BarcodeEventArgs e)
        {
            if (!isScanning || e.Result == null || e.Result.Count == 0)
                return;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    // Αναστολή σάρωσης για αποφυγή διπλών αποτελεσμάτων
                    cameraView.BarCodeDetected -= CameraView_BarCodeDetected;
                    isScanning = false;

                    // Εξαγωγή του κειμένου από το barcode (θα χρησιμοποιηθεί ως MRZ)
                    string rawText = e.Result[0].Text;

                    // Για αληθινό MRZ scanning, θα προσθέταμε εδώ ειδική επεξεργασία
                    // Επειδή η ZXing αναγνωρίζει barcodes και όχι MRZ, θα πρέπει να δημιουργήσουμε 
                    // ένα συνθετικό MRZ για δοκιμαστικούς σκοπούς

                    // Για την επίδειξη, δημιουργούμε ένα MRZ αντικείμενο
                    var mrzData = new MrzData
                    {
                        DeviceId = App.DeviceId,
                        TerminalId = TerminalId,
                        DocumentType = "P",
                        LastName = "SMITH",
                        FirstName = "JOHN",
                        DocumentNumber = rawText.Length > 8 ? rawText.Substring(0, 8) : "X12345678",
                        Nationality = "GRC",
                        Sex = "M",
                        BirthDate = new DateTime(1990, 1, 1),
                        ExpirationDate = new DateTime(2030, 1, 1),
                        IssuingCountry = "GRC",
                        RawMrzData = rawText
                    };

                    // Αποστολή στο SignalR hub
                    if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
                    {
                        await hubConnection.InvokeAsync("SendMrzData", mrzData);

                        statusLabel.Text = "Document scanned successfully!";

                        // Παύση για να δει ο χρήστης το μήνυμα επιτυχίας
                        await Task.Delay(2000);

                        // Επιστροφή στην προηγούμενη σελίδα
                        await Shell.Current.GoToAsync("..");
                    }
                    else
                    {
                        throw new Exception("Connection to server lost");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to process scan: {ex.Message}", "OK");

                    // Επανεκκίνηση της σάρωσης
                    cameraView.BarCodeDetected += CameraView_BarCodeDetected;
                    isScanning = true;
                }
            });
        }

        private async void DoneButton_Clicked(object sender, EventArgs e)
        {
            StopScanning();
            await Shell.Current.GoToAsync("..");
        }
    }
}