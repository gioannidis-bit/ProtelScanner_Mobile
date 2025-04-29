using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.MediaElement;
using ZXing.Net.Maui;
using Camera.MAUI;
using ProtelScanner.Mobile.Services;
using Microsoft.Maui.Controls.Hosting;

namespace ProtelScanner.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement()
            .UseZXingNetMaui()
            .UseMauiCameraView()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Register scanner service for DI
        builder.Services.AddSingleton<IMrzScannerService, MrzScannerService>();

        return builder.Build();
    }
}