using Microsoft.Maui.Controls;

namespace ProtelScanner.Mobile
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Καταχώριση των routes για την πλοήγηση
            Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
            Routing.RegisterRoute(nameof(ScannerPage), typeof(ScannerPage));
        }
    }
}