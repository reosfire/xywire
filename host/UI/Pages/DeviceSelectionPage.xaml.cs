using Leds.services;

namespace UI.Pages;

public partial class DeviceSelectionPage : ContentPage
{
    private readonly DeviceService _deviceService;
    private readonly EffectService _effectService;
    private List<string> _devices;
    public event Action<bool>? SuccessfulConnectionEvent; 

    public DeviceSelectionPage(DeviceService deviceService, EffectService effectService, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _deviceService = deviceService;
        _effectService = effectService;
        _devices = [];
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
        string? deviceAddress = bindable.BindingContext as string;
        if (string.IsNullOrEmpty(deviceAddress)) return;

        try
        {
            LoadingMessage.Text = $"Connecting to {deviceAddress}...";
            LoadingOverlay.IsVisible = true;
            
            await Task.Run(() => _effectService.ConnectToDevice(deviceAddress));
            
            LoadingOverlay.IsVisible = false;
            
            await Navigation.PopAsync();
            SuccessfulConnectionEvent?.Invoke(true);
        }
        catch (Exception ex)
        {
            // Hide loading overlay on error
            LoadingOverlay.IsVisible = false;
            await DisplayAlertAsync("Error", $"Failed to connect: {ex.Message}", "OK");
        }
    }

    private async void OnAddDeviceClicked(object sender, EventArgs e)
    {
        string? address = DeviceAddressEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(address))
        {
            await DisplayAlertAsync("Invalid Input", "Please enter a device address.", "OK");
            return;
        }

        try
        {
            _deviceService.AddDevice(address);
            DeviceAddressEntry.Text = string.Empty;
            LoadDevices();
            await DisplayAlertAsync("Success", $"Device {address} added successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to add device: {ex.Message}", "OK");
        }
    }

    private async void OnDeleteDevice(object sender, EventArgs e)
    {
        if (sender is not SwipeItem swipeItem) return;
        string? deviceAddress = swipeItem.BindingContext as string;
        if (string.IsNullOrEmpty(deviceAddress)) return;

        bool result = await DisplayAlertAsync("Confirm Delete",
            $"Are you sure you want to delete {deviceAddress}?",
            "Yes", "No");

        if (result)
        {
            _deviceService.RemoveDevice(deviceAddress);
            LoadDevices();
        }
    }
}
