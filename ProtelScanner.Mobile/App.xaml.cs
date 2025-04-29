using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace ProtelScanner.Mobile
{
    public partial class App : Application
    {
        // Ιδιότητες ρυθμίσεων της εφαρμογής
        public static string ServerUrl { get; set; }
        public static string DeviceName { get; set; }
        public static string DeviceId { get; set; }

        public App()
        {
            InitializeComponent();
            LoadSettings();
            MainPage = new AppShell();
        }

        private void LoadSettings()
        {
            // Φόρτωση ρυθμίσεων από αποθηκευμένες προτιμήσεις
            ServerUrl = Preferences.Get("ServerUrl", string.Empty);
            DeviceName = Preferences.Get("DeviceName", DeviceInfo.Name);
            DeviceId = Preferences.Get("DeviceId", null);
        }

        public static void SaveSettings()
        {
            // Αποθήκευση ρυθμίσεων
            Preferences.Set("ServerUrl", ServerUrl);
            Preferences.Set("DeviceName", DeviceName);
            if (!string.IsNullOrEmpty(DeviceId))
                Preferences.Set("DeviceId", DeviceId);
        }
    }
}