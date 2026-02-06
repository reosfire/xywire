using Microsoft.Extensions.Logging;
using XywireHost.Core.services;
using XywireHost.UI.Pages;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace XywireHost.UI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Register services
        builder.Services.AddSingleton<DeviceService>();
        builder.Services.AddSingleton<BluetoothService>();
        builder.Services.AddSingleton<EffectService>();

        // Register pages
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<DeviceSelectionPage>();
        builder.Services.AddTransient<EffectControlPage>();
        builder.Services.AddTransient<WiFiSetupPage>();
        builder.Services.AddTransient<NodeEditorPage>();

        return builder.Build();
    }
}
