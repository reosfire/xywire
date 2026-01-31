using Leds.services;

namespace UI.Pages;

public partial class EffectControlPage : ContentPage
{
    private readonly EffectService _effectService;
    private List<EffectInfo> _availableEffects;

    public EffectControlPage(EffectService effectService)
    {
        InitializeComponent();
        _effectService = effectService;
        _availableEffects = new List<EffectInfo>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadEffects();
        UpdateStatus();
    }

    private void LoadEffects()
    {
        _availableEffects = _effectService.GetAvailableEffects();
        EffectsCollectionView.ItemsSource = _availableEffects;
    }

    private async void OnEffectSelected(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable) return;
        var effectInfo = bindable.BindingContext as EffectInfo;
        if (effectInfo == null) return;

        try
        {
            _effectService.StartEffect(effectInfo);
            StatusLabel.Text = $"Running: {effectInfo.Name}";
            await DisplayAlertAsync("Success", $"Started effect: {effectInfo.Name}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to start effect: {ex.Message}", "OK");
        }
    }

    private void OnStopEffectClicked(object sender, EventArgs e)
    {
        _effectService.StopCurrentEffect();
        StatusLabel.Text = "No effect running";
    }

    private void OnBrightnessChanged(object sender, ValueChangedEventArgs e)
    {
        var brightness = (int)e.NewValue;
        BrightnessLabel.Text = brightness.ToString();
        _effectService.SetBrightness(brightness);
    }

    private void UpdateStatus()
    {
        StatusLabel.Text = _effectService.IsEffectRunning 
            ? "Effect is running" 
            : "No effect running";
    }
}
