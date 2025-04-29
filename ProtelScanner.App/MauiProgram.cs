using Microsoft.Maui;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using ZXing.Net.Maui;              // ← this is critical

namespace ProtelScanner.App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
              .UseMauiApp<App>()
              .UseBarcodeReader()         // ← THIS registers CameraBarcodeReaderView
              .ConfigureFonts(fonts =>
              {
                  fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
              });

            return builder.Build();
        }
    }
}
