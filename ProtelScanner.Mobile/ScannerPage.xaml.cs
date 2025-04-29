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

            // �������� ��� hub connection
            hubConnection = App.Current.Properties["HubConnection"] as HubConnection;

            if (hubConnection == null || hubConnection.State != HubConnectionState.Connected)
            {
                await DisplayAlert("Error", "Connection to server lost", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // ������� ����������� �������
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

            // ������ �������
            await StartScanningAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // ������� ������� ���� ������� � ������
            StopScanning();
        }

        private async Task StartScanningAsync()
        {
            try
            {
                isScanning = true;
                statusLabel.Text = "Scanning in progress...";

                // ������� ��� event ��� ���� ������ MRZ
                scannerService.MrzDetected += OnMrzDetected;

                // ������ ��� �������
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

            // �������� ������� ��� ������� ������ �������������
            StopScanning();

            try
            {
                // �������� ��� ����������� ����������� ��� MRZ
                mrzData.DeviceId = App.DeviceId;
                mrzData.TerminalId = TerminalId;

                // �������� ��� ��������� ��� SignalR hub
                if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
                {
                    await hubConnection.InvokeAsync("SendMrzData", mrzData);

                    statusLabel.Text = "MRZ Detected and Sent!";

                    // ����� ��� �� ��� � ������� �� ������ ���������
                    await Task.Delay(2000);

                    // ��������� ���� ����������� ������
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
                // ������������ ��� ������� �� ��������� ���������
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