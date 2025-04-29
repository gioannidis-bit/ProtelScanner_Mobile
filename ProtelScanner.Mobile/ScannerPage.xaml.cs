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

            // ���� ��� ���������� ���������
            if (Shell.Current.CurrentState.RouteParameters.TryGetValue("TerminalId", out var terminalId))
            {
                _terminalId = terminalId.ToString();
            }

            // ���������� ��������� ��� ���������� hub connection
            if (_hubConnection == null && AppStateManager.ContainsKey("HubConnection"))
            {
                _hubConnection = AppStateManager.GetValue<HubConnection>("HubConnection");
            }

            // ������ ��� ������� ��� ��� scanning
            try
            {
                await _mrzScannerService.StartScanningAsync(cameraView);
                statusLabel.Text = "������ �� �������...";
            }
            catch (Exception ex)
            {
                await DisplayAlert("������", $"������ ���� ��� ������ ��� �������: {ex.Message}", "OK");
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
            // �������� ��� ����� thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    // ��������� ��� UI
                    statusLabel.Text = "MRZ �����������!";

                    // �������� ��� ��������� ������������
                    mrzData.DeviceId = App.DeviceId;
                    mrzData.TerminalId = _terminalId;

                    // �������� ��� ��������� ���� server �� ������ ������ �������
                    if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                    {
                        await _hubConnection.InvokeAsync("SendMrzData", mrzData);
                        statusLabel.Text = "�� �������� ��������� ��������!";
                    }
                }
                catch (Exception ex)
                {
                    statusLabel.Text = $"������: {ex.Message}";
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