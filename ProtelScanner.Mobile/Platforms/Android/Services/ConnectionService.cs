using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Content;
using Microsoft.AspNetCore.SignalR.Client;

namespace ProtelScanner.Mobile.Platforms.Android.Services
{
    // ProtelScanner.Mobile.Platforms.Android.Services.ConnectionService.cs
    public class ConnectionService
    {
        private readonly HubConnection hubConnection;
        private readonly Context context;

        public ConnectionService(Context context, string serverUrl)
        {
            this.context = context;

            hubConnection = new HubConnectionBuilder()
                .WithUrl($"{serverUrl}/scannerhub")
                .WithAutomaticReconnect()
                .Build();

            SetupHubEvents();
        }

        private void SetupHubEvents()
        {
            hubConnection.On("Wakeup", () =>
            {
                // Εκκίνηση της εφαρμογής και μετάβαση στην οθόνη σάρωσης
                var intent = new Intent(context, typeof(MainActivity));
                intent.SetFlags(ActivityFlags.NewTask);
                intent.PutExtra("navigate_to_scanner", true);
                context.StartActivity(intent);
            });
        }

        public async Task StartAsync()
        {
            await hubConnection.StartAsync();
            await hubConnection.InvokeAsync("RegisterDevice", DeviceInfo.Name);
        }

        public async Task StopAsync()
        {
            await hubConnection.StopAsync();
        }
    }
}
