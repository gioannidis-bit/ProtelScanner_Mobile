using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace ProtelScanner.Mobile
{
    public partial class MainPage : ContentPage
    {
        private HubConnection hubConnection;
        private bool isConnected = false;
        private string reservedBy = null;

        public MainPage()
        {
            InitializeComponent();

            // Εκκίνηση με φόρτωση των τρεχουσών ρυθμίσεων
            serverUrlEntry.Text = App.ServerUrl;
            deviceNameEntry.Text = App.DeviceName;

            UpdateStatusLabel();
        }

        private async void ConnectButton_Clicked(object sender, EventArgs e)
        {
            if (isConnected)
            {
                await DisconnectFromServer();
            }
            else
            {
                await ConnectToServer();
            }
        }

        private async Task ConnectToServer()
        {
            if (string.IsNullOrWhiteSpace(serverUrlEntry.Text))
            {
                await DisplayAlert("Error", "Please enter a server URL", "OK");
                return;
            }

            // Αποθήκευση ρυθμίσεων
            App.ServerUrl = serverUrlEntry.Text.Trim();
            App.DeviceName = deviceNameEntry.Text.Trim();
            App.SaveSettings();

            // Απενεργοποίηση UI κατά τη σύνδεση
            connectButton.IsEnabled = false;
            statusLabel.Text = "Connecting...";

            try
            {
                // Δημιουργία σύνδεσης SignalR
                hubConnection = new HubConnectionBuilder()
                    .WithUrl($"{App.ServerUrl}/scannerhub")
                    .WithAutomaticReconnect()
                    .Build();

                // Διαχείριση των events από το hub
                SetupHubEvents();

                // Εκκίνηση σύνδεσης
                await hubConnection.StartAsync();

                // Εγγραφή της συσκευής
                await hubConnection.InvokeAsync("RegisterDevice", App.DeviceName);

                isConnected = true;
                UpdateUI();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Connection Error", $"Failed to connect: {ex.Message}", "OK");
                isConnected = false;
            }
            finally
            {
                connectButton.IsEnabled = true;
                UpdateStatusLabel();
            }
        }

        private void SetupHubEvents()
        {
            // Χειρισμός εγγραφής συσκευής
            hubConnection.On<string>("DeviceRegistered", (deviceId) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    App.DeviceId = deviceId;
                    App.SaveSettings();
                });
            });

            // Χειρισμός εντολής αφύπνισης
            hubConnection.On("Wakeup", () =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (!string.IsNullOrEmpty(reservedBy))
                    {
                        await NavigateToScannerPage();
                    }
                });
            });

            // Χειρισμός δέσμευσης συσκευής
            hubConnection.On<string>("ReservedBy", (terminalId) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    reservedBy = terminalId;
                    UpdateStatusLabel();
                    UpdateUI();
                });
            });

            // Χειρισμός αποδέσμευσης συσκευής
            hubConnection.On("Released", () =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    reservedBy = null;
                    UpdateStatusLabel();
                    UpdateUI();
                });
            });
        }

        private async Task NavigateToScannerPage()
        {
            if (string.IsNullOrEmpty(App.DeviceId) || string.IsNullOrEmpty(reservedBy))
                return;

            var navigationParams = new Dictionary<string, object>
            {
                { "TerminalId", reservedBy }
            };

            await Shell.Current.GoToAsync($"{nameof(ScannerPage)}", navigationParams);
        }

        private async Task DisconnectFromServer()
        {
            if (hubConnection != null)
            {
                try
                {
                    await hubConnection.StopAsync();
                    hubConnection = null;
                    isConnected = false;
                    reservedBy = null;
                    UpdateUI();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Disconnection Error", $"Error disconnecting: {ex.Message}", "OK");
                }
            }
        }

        private void UpdateUI()
        {
            serverUrlEntry.IsEnabled = !isConnected;
            deviceNameEntry.IsEnabled = !isConnected;
            connectButton.Text = isConnected ? "Disconnect" : "Connect";
            scanButton.IsEnabled = isConnected && !string.IsNullOrEmpty(reservedBy);

            UpdateStatusLabel();
        }

        private void UpdateStatusLabel()
        {
            if (!isConnected)
            {
                statusLabel.Text = "Status: Disconnected";
                return;
            }

            if (string.IsNullOrEmpty(reservedBy))
            {
                statusLabel.Text = "Status: Connected";
            }
            else
            {
                statusLabel.Text = $"Status: Reserved by {reservedBy}";
            }
        }

        private async void ScanButton_Clicked(object sender, EventArgs e)
        {
            if (isConnected && !string.IsNullOrEmpty(reservedBy))
            {
                await NavigateToScannerPage();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Συντήρηση του hub connection κατά την πλοήγηση
            if (isConnected && hubConnection != null)
            {
                AppStateManager.SetValue("HubConnection", hubConnection);
            }
        }
    }
}