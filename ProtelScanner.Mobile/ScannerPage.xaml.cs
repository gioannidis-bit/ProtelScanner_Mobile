using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Maui.Controls;
using ProtelScanner.Mobile.Models;
using ProtelScanner.Mobile.Services;
using Microsoft.Maui.ApplicationModel;

namespace ProtelScanner.Mobile
{
    public partial class ScannerPage : ContentPage
    {
        private readonly IMrzScannerService _mrzScannerService;
        private string _terminalId;
        private HubConnection _hubConnection;

        public ScannerPage(IMrzScannerService mrzScannerService)
        {
            InitializeComponent();
            _mrzScannerService = mrzScannerService;
            _mrzScannerService.MrzDetected += OnMrzDetected;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Λήψη των παραμέτρων πλοήγησης
            if (Shell.Current.CurrentState.RouteParameters.TryGetValue("TerminalId", out var terminalId))
            {
                _terminalId = terminalId.ToString();
            }

            // Προσπάθεια ανάκτησης του υπάρχοντος hub connection
            if (_hubConnection == null && AppStateManager.ContainsKey("HubConnection"))
            {
                _hubConnection = AppStateManager.GetValue<HubConnection>("HubConnection");
            }

            // Έναρξη της κάμερας και του scanning
            try
            {
                await _mrzScannerService.StartScanningAsync(cameraView);
                statusLabel.Text = "Σάρωση σε εξέλιξη...";
            }
            catch (Exception ex)
            {
                await DisplayAlert("Σφάλμα", $"Σφάλμα κατά την έναρξη της κάμερας: {ex.Message}", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _mrzScannerService.StopScanning();
            _mrzScannerService.MrzDetected -= OnMrzDetected;
        }

        private async void OnMrzDetected(object sender, MrzData mrzData)
        {
            // Εκτέλεση στο κύριο thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    // Ενημέρωση του UI
                    statusLabel.Text = "MRZ Εντοπίστηκε!";

                    // Προσθήκη των στοιχείων ταυτοποίησης
                    mrzData.DeviceId = App.DeviceId;
                    mrzData.TerminalId = _terminalId;

                    // Αποστολή των δεδομένων στον server αν έχουμε ενεργή σύνδεση
                    if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                    {
                        await _hubConnection.InvokeAsync("SendMrzData", mrzData);
                        statusLabel.Text = "Τα δεδομένα στάλθηκαν επιτυχώς!";
                    }
                }
                catch (Exception ex)
                {
                    statusLabel.Text = $"Σφάλμα: {ex.Message}";
                }
            });
        }

        private async void DoneButton_Clicked(object sender, EventArgs e)
        {
            _mrzScannerService.StopScanning();
            await Shell.Current.GoToAsync("..");
        }
    }
}