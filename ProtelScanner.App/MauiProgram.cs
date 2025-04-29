using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using ZXing.Net.Maui.Controls;

namespace ProtelScanner.App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureMauiHandlers(handlers =>
                {
                    handlers.AddHandler<ZXing.Net.Maui.Controls.CameraBarcodeReaderView,
                                       ZXing.Net.Maui.Handlers.CameraBarcodeReaderViewHandler>();
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            return builder.Build();
        }
    }
}