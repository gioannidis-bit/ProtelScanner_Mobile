using Microsoft.Maui.Controls;

namespace ProtelScanner.Mobile;

public partial class MainPage : ContentPage
{
    public MainPage() => InitializeComponent();

    async void OnOpenScanner(object s, EventArgs e)
    {
        // περάσε ό,τι παράμετρο θέλεις
        await Shell.Current.GoToAsync($"{nameof(ScannerPage)}?TerminalId=123");
    }
}
