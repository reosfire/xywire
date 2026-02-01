using Leds.services;

namespace UI.Pages;

public partial class MainPage : ContentPage
{
    private readonly EffectService _effectService;
    private readonly IServiceProvider _serviceProvider;

    public MainPage(
        EffectService effectService,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _effectService = effectService;
        _serviceProvider = serviceProvider;

        BindingContext = this;
    }

    public bool IsConnected => _effectService.IsConnected;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateStatus();
    }

    private async void OnConnectDeviceClicked(object sender, EventArgs e)
    {
        DeviceSelectionPage page = _serviceProvider.GetRequiredService<DeviceSelectionPage>();
        await Navigation.PushAsync(page);
    }

    private async void OnWiFiSetupClicked(object sender, EventArgs e)
    {
        WiFiSetupPage page = _serviceProvider.GetRequiredService<WiFiSetupPage>();
        await Navigation.PushAsync(page);
    }

    private async void OnEffectControlClicked(object sender, EventArgs e)
    {
        if (_effectService.IsConnected)
        {
            EffectControlPage page = _serviceProvider.GetRequiredService<EffectControlPage>();
            await Navigation.PushAsync(page);
        }
        else
        {
            await DisplayAlertAsync("Not Connected", "Please connect to a device first.", "OK");
        }
    }

    private void UpdateStatus()
    {
        StatusLabel.Text = _effectService.IsConnected ? "Connected to device" : "Not connected";
        OnPropertyChanged(nameof(IsConnected));
    }
}
