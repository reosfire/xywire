using Leds.services;

namespace UI.Pages;

public partial class DeviceSelectionPage : ContentPage
{
    private readonly DeviceService _deviceService;
    private readonly EffectService _effectService;
    private List<string> _devices;

    public DeviceSelectionPage(DeviceService deviceService, EffectService effectService)
    {
        InitializeComponent();
        _deviceService = deviceService;
        _effectService = effectService;
        _devices = new List<string>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadDevices();
    }

    private void LoadDevices()
    {
        _devices = _deviceService.LoadSavedDevices();
        DevicesCollectionView.ItemsSource = _devices;
    }

    private async void OnDeviceSelected(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable) return;
        var deviceAddress = bindable.BindingContext as string;
        if (string.IsNullOrEmpty(deviceAddress)) return;

        try
        {
            var connectButton = new Button { Text = "Connecting..." };
            await DisplayAlert("Connecting", $"Connecting to {deviceAddress}...", "Cancel");
            
            _effectService.ConnectToDevice(deviceAddress, 100);
            await DisplayAlert("Success", $"Connected to {deviceAddress}", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to connect: {ex.Message}", "OK");
        }
    }

    private async void OnAddDeviceClicked(object sender, EventArgs e)
    {
        var address = DeviceAddressEntry.Text?.Trim();
        
        if (string.IsNullOrWhiteSpace(address))
        {
            await DisplayAlert("Invalid Input", "Please enter a device address.", "OK");
            return;
        }

        try
        {
            _deviceService.AddDevice(address);
            DeviceAddressEntry.Text = string.Empty;
            LoadDevices();
            await DisplayAlert("Success", $"Device {address} added successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to add device: {ex.Message}", "OK");
        }
    }

    private async void OnDeleteDevice(object sender, EventArgs e)
    {
        if (sender is not SwipeItem swipeItem) return;
        var deviceAddress = swipeItem.BindingContext as string;
        if (string.IsNullOrEmpty(deviceAddress)) return;

        var result = await DisplayAlert("Confirm Delete", 
            $"Are you sure you want to delete {deviceAddress}?", 
            "Yes", "No");

        if (result)
        {
            _deviceService.RemoveDevice(deviceAddress);
            LoadDevices();
        }
    }
}
