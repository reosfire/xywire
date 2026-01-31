using Leds.services;

namespace UI.Pages;

public partial class WiFiSetupPage : ContentPage
{
    private readonly Leds.services.BluetoothService _bluetoothService;
    private List<BluetoothDeviceDto> _discoveredDevices = new();

    public WiFiSetupPage(Leds.services.BluetoothService bluetoothService)
    {
        InitializeComponent();
        _bluetoothService = bluetoothService;
    }

    private async void OnScanClicked(object sender, EventArgs e)
    {
        ScanButton.IsEnabled = false;
        ScanningIndicator.IsRunning = true;
        ScanningIndicator.IsVisible = true;
        ScanStatusLabel.Text = "Scanning for Bluetooth devices...";

        try
        {
            _discoveredDevices = await _bluetoothService.DiscoverDevicesAsync();
            
            if (_discoveredDevices.Count == 0)
            {
                ScanStatusLabel.Text = "No Bluetooth devices found";
            }
            else
            {
                ScanStatusLabel.Text = $"Found {_discoveredDevices.Count} device(s)";
                BluetoothDevicesCollectionView.ItemsSource = _discoveredDevices;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to scan for devices: {ex.Message}", "OK");
            ScanStatusLabel.Text = "Scan failed";
        }
        finally
        {
            ScanningIndicator.IsRunning = false;
            ScanningIndicator.IsVisible = false;
            ScanButton.IsEnabled = true;
        }
    }

    private async void OnBluetoothDeviceSelected(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable) return;
        var device = bindable.BindingContext as BluetoothDeviceDto;
        if (device == null) return;

        var ssid = await DisplayPromptAsync("WiFi SSID", "Enter the WiFi network name (SSID):");
        if (string.IsNullOrWhiteSpace(ssid)) return;

        var password = await DisplayPromptAsync("WiFi Password", "Enter the WiFi password:", keyboard: Keyboard.Default);
        if (password == null) return;

        try
        {
            await DisplayAlert("Sending", "Sending WiFi credentials...", "OK");
            await _bluetoothService.SendWiFiCredentialsAsync(device, ssid, password);
            await DisplayAlert("Success", "WiFi credentials sent successfully!", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to send credentials: {ex.Message}", "OK");
        }
    }
}
