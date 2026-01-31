using Microsoft.Extensions.Logging;
using Leds.services;
using UI.Pages;

namespace UI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
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

        return builder.Build();
    }
}