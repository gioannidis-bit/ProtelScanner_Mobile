using System;
using Microsoft.Maui.Controls;

namespace ProtelScanner.App
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnScanClicked(object sender, EventArgs e)
        {
            // navigate to the camera scanner page
            await Navigation.PushAsync(new ScannerPage());
        }
    }
}
