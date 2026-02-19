using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using XywireHost.Core.Graph;
using XywireHost.Core.services;
using XywireHost.UI.Services;

namespace XywireHost.UI.Pages;

public partial class PluginSettingsPage : ContentPage
{
    private readonly PluginService _pluginService;
    private readonly ObservableCollection<string> _pluginPaths = [];

    public PluginSettingsPage(PluginService pluginService)
    {
        InitializeComponent();
        _pluginService = pluginService;

        PluginPathsCollectionView.ItemsSource = _pluginPaths;

        _pluginService.PluginPathsChanged += OnPluginPathsChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LoadPluginPaths();
        UpdatePluginCount();
        
#if ANDROID
        await CheckAndRequestPermissions();
#endif
    }

#if ANDROID
    private async Task CheckAndRequestPermissions()
    {
        bool hasPermission = await PermissionService.CheckStoragePermission();
        if (!hasPermission)
        {
            bool granted = await PermissionService.CheckAndRequestStoragePermission();
            if (!granted)
            {
                await DisplayAlertAsync(
                    "Permission Required",
                    "Storage permission is required to load plugin files. Some features may not work correctly.",
                    "OK");
            }
        }
    }
#endif

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _pluginService.PluginPathsChanged -= OnPluginPathsChanged;
    }

    private void LoadPluginPaths()
    {
        _pluginPaths.Clear();
        foreach (string path in _pluginService.PluginPaths)
        {
            _pluginPaths.Add(path);
        }

        NoPathsLabel.IsVisible = _pluginPaths.Count == 0;
    }

    private void OnPluginPathsChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadPluginPaths();
            UpdatePluginCount();
        });
    }

    private void UpdatePluginCount()
    {
        int concreteCount = EffectNodeCatalog.All.Count;
        int genericCount = EffectNodeCatalog.AllGeneric.Count;
        PluginCountLabel.Text = $"{concreteCount} concrete, {genericCount} generic";
    }

    private async void OnAddFolderClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            // Check permissions first on Android
            bool hasPermission = await PermissionService.CheckAndRequestStoragePermission();
            if (!hasPermission)
            {
                await DisplayAlertAsync(
                    "Permission Denied",
                    "Storage permission is required to access plugin folders.",
                    "OK");
                return;
            }
#endif

            var result = await FolderPicker.Default.PickAsync(default);
            
            if (result.IsSuccessful && !string.IsNullOrWhiteSpace(result.Folder?.Path))
            {
                string folderPath = result.Folder.Path;
                _pluginService.AddPluginPath(folderPath);
                await DisplayAlertAsync("Success", $"Plugin folder added:\n{folderPath}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to add folder: {ex.Message}", "OK");
        }
    }

    private async void OnAddFileClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            // Check permissions first on Android
            bool hasPermission = await PermissionService.CheckAndRequestStoragePermission();
            if (!hasPermission)
            {
                await DisplayAlertAsync(
                    "Permission Denied",
                    "Storage permission is required to access plugin files.",
                    "OK");
                return;
            }
#endif

            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.dll" } },
                    { DevicePlatform.Android, new[] { "application/octet-stream" } },
                    { DevicePlatform.WinUI, new[] { ".dll" } },
                    { DevicePlatform.macOS, new[] { "dll" } },
                });

            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Plugin DLL File"
            });

            if (result != null && !string.IsNullOrWhiteSpace(result.FullPath))
            {
                if (result.FullPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    _pluginService.AddPluginPath(result.FullPath);
                    await DisplayAlertAsync("Success", $"Plugin file added:\n{result.FullPath}", "OK");
                }
                else
                {
                    await DisplayAlertAsync("Error", "Selected file is not a DLL.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to add file: {ex.Message}", "OK");
        }
    }

    private async void OnReloadPluginsClicked(object sender, EventArgs e)
    {
        try
        {
            EffectNodeCatalog.ClearPlugins();
            EffectNodeCatalog.LoadPluginsFromPaths(_pluginService.PluginPaths);
            UpdatePluginCount();
            await DisplayAlertAsync("Success", "Plugins reloaded successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to reload plugins: {ex.Message}", "OK");
        }
    }

    private async void OnDeletePluginPath(object sender, EventArgs e)
    {
        try
        {
            if (sender is not SwipeItem swipeItem) return;
            if (swipeItem.BindingContext is not string path) return;

            bool confirm = await DisplayAlertAsync(
                "Confirm Delete",
                $"Remove this plugin path?\n\n{path}",
                "Delete",
                "Cancel");

            if (confirm)
            {
                _pluginService.RemovePluginPath(path);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to delete plugin path: {ex.Message}", "OK");
        }
    }
}
