using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Maui.Controls;
using ZXing.Net.Maui;

namespace ProtelScanner.App
{
    public partial class ScannerPage : ContentPage
    {
        const string BASE_URL = "https://your.server.com/";  // ← your existing endpoint

        public ScannerPage()
        {
            InitializeComponent();

            // ensure continuous detection is on
            BarcodeReaderView.IsDetecting = true;
        }

        private async void BarcodeReaderView_BarcodeDetected(object sender, BarcodeDetectionEventArgs e)
        {
            // stop further detections
            BarcodeReaderView.IsDetecting = false;

            var first = e.Results.FirstOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(first))
            {
                await DisplayAlert("Error", "No barcode found.", "OK");
                return;
            }

            // show raw MRZ for debug
            await DisplayAlert("Raw MRZ", first, "OK");

            // parse it
            var mrzData = MrzParser.Parse(first);

            // post to server
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri(BASE_URL) };
                var resp = await client.PostAsJsonAsync("api/scan/mrz", mrzData);

                if (resp.IsSuccessStatusCode)
                {
                    var serverRes = await resp.Content.ReadFromJsonAsync<ServerResponse>();
                    await DisplayAlert("Success", serverRes.Message, "OK");
                }
                else
                {
                    await DisplayAlert("Server Error", resp.StatusCode.ToString(), "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Network Error", ex.Message, "OK");
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            BarcodeReaderView.IsDetecting = true;
        }

        protected override void OnDisappearing()
        {
            BarcodeReaderView.IsDetecting = false;
            base.OnDisappearing();
        }
    }
}
