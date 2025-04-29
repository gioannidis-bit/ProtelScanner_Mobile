using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Maui.Controls;
using System.Collections.Generic;
using ProtelScanner.Mobile.Services;
using ProtelScanner.Mobile.Models;
using Camera.MAUI;

namespace ProtelScanner.Mobile
{
    [QueryProperty(nameof(TerminalId), "TerminalId")]
    public partial class ScannerPage : ContentPage
    {
        private HubConnection hubConnection;
        private IMrzScannerService scannerService;
        private bool isScanning = false;

        public string TerminalId { get; set; }

        public ScannerPage(IMrzScannerService scannerService)
        {
            InitializeComponent();
            this.scannerService = scannerService;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Ανάκτηση του hub connection
            hubConnection = App.Current.Properties["HubConnection"] as HubConnection;

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

            // Έναρξη σάρωσης
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

                // Εγγραφή στο event για όταν βρεθεί MRZ
                scannerService.MrzDetected += OnMrzDetected;

                // Έναρξη της σάρωσης
                await scannerService.StartScanningAsync(cameraView);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Scanner Error", $"Failed to start scanner: {ex.Message}", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }

        private void StopScanning()
        {
            if (isScanning)
            {
                isScanning = false;
                scannerService.MrzDetected -= OnMrzDetected;
                scannerService.StopScanning();
            }
        }

        private async void OnMrzDetected(object sender, MrzData mrzData)
        {
            if (!isScanning)
                return;

            // Αναστολή σάρωσης για αποφυγή διπλών αποτελεσμάτων
            StopScanning();

            try
            {
                // Προσθήκη των απαραίτητων πληροφοριών στο MRZ
                mrzData.DeviceId = App.DeviceId;
                mrzData.TerminalId = TerminalId;

                // Αποστολή των δεδομένων στο SignalR hub
                if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
                {
                    await hubConnection.InvokeAsync("SendMrzData", mrzData);

                    statusLabel.Text = "MRZ Detected and Sent!";

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
                await DisplayAlert("Error", $"Failed to send MRZ data: {ex.Message}", "OK");
                // Επανεκκίνηση της σάρωσης σε περίπτωση σφάλματος
                await StartScanningAsync();
            }
        }

        private async void DoneButton_Clicked(object sender, EventArgs e)
        {
            StopScanning();
            await Shell.Current.GoToAsync("..");
        }
    }
}