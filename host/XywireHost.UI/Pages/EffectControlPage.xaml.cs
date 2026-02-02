using System.Collections.ObjectModel;
using System.ComponentModel;
using XywireHost.Core.services;

namespace XywireHost.UI.Pages;

public partial class EffectControlPage : ContentPage
{
    private readonly EffectService _effectService;
    private readonly ObservableCollection<EffectItemViewModel> _effectViewModels;
    private EffectInfo? _currentlyPlayingEffect;

    public EffectControlPage(EffectService effectService)
    {
        InitializeComponent();
        _effectService = effectService;
        _effectViewModels = [];
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadEffects();
        UpdateStatus();
    }

    private void LoadEffects()
    {
        List<EffectInfo> availableEffects = EffectService.GetAvailableEffects();
        _effectViewModels.Clear();

        foreach (EffectInfo effect in availableEffects)
        {
            _effectViewModels.Add(new EffectItemViewModel { EffectInfo = effect, IsPlaying = false });
        }

        EffectsCollectionView.ItemsSource = _effectViewModels;
    }

    private async void OnEffectSelected(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable) return;
        if (bindable.BindingContext is not EffectItemViewModel viewModel) return;

        try
        {
            EffectItemViewModel? previousViewModel =
                _effectViewModels.FirstOrDefault(vm => vm.EffectInfo == _currentlyPlayingEffect);
            previousViewModel?.IsPlaying = false;

            await _effectService.StartEffect(viewModel.EffectInfo);
            _currentlyPlayingEffect = viewModel.EffectInfo;

            viewModel.IsPlaying = true;

            StatusLabel.Text = $"Running: {viewModel.EffectInfo.Name}";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to start effect: {ex.Message}", "OK");
        }
    }

    private async void OnStopEffectClicked(object sender, EventArgs e)
    {
        if (_currentlyPlayingEffect != null)
        {
            EffectItemViewModel? previousViewModel =
                _effectViewModels.FirstOrDefault(vm => vm.EffectInfo == _currentlyPlayingEffect);
            previousViewModel?.IsPlaying = false;
        }

        await _effectService.StopCurrentEffect();
        _currentlyPlayingEffect = null;
        StatusLabel.Text = "No effect running";
    }

    private async void OnBrightnessChanged(object sender, ValueChangedEventArgs e)
    {
        byte brightness = (byte)(int)e.NewValue;
        BrightnessLabel.Text = brightness.ToString();
        Task? setBrightnessTask = _effectService.SetBrightness(brightness);
        if (setBrightnessTask != null) await setBrightnessTask;
    }

    private void UpdateStatus()
    {
        StatusLabel.Text = _effectService.IsEffectRunning
            ? "Effect is running"
            : "No effect running";
    }
}

public class EffectItemViewModel : INotifyPropertyChanged
{
    public required EffectInfo EffectInfo { get; init; }

    public string Name => EffectInfo.Name;

    public bool IsPlaying
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPlaying)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundColor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StrokeColor)));
        }
    }

    public Color BackgroundColor => IsPlaying ? Colors.LightGreen.MultiplyAlpha(0.5f) : Colors.Transparent;
    public Color StrokeColor => IsPlaying ? Colors.Green : Colors.LightGray;

    public event PropertyChangedEventHandler? PropertyChanged;
}
