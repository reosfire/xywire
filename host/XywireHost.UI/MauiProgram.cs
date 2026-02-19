using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using XywireHost.Core.Graph;
using XywireHost.Core.services;
using XywireHost.UI.Pages;
using XywireHost.UI.Services;

namespace XywireHost.UI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
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
        builder.Services.AddSingleton<PluginService>(sp => new MauiPluginService());

        // Register pages
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<DeviceSelectionPage>();
        builder.Services.AddTransient<EffectControlPage>();
        builder.Services.AddTransient<WiFiSetupPage>();
        builder.Services.AddTransient<NodeEditorPage>();
        builder.Services.AddTransient<PluginSettingsPage>();

        MauiApp app = builder.Build();

        // Load plugins on startup
        PluginService pluginService = app.Services.GetRequiredService<PluginService>();
        EffectNodeCatalog.LoadPluginsFromPaths(pluginService.PluginPaths);

        return app;
    }
}
