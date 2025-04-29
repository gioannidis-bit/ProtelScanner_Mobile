using Microsoft.Maui.Controls;

namespace ProtelScanner.Mobile;



public partial class ScannerPage : ContentPage, IQueryAttributable
{
    void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("TerminalId", out var id))
            _terminalId = id?.ToString();
    }
}




public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(ScannerPage), typeof(ScannerPage));
        await Shell.Current.GoToAsync($"{nameof(ScannerPage)}?TerminalId={_terminalId}");

    }
}
