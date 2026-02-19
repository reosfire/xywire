using System.Text.Json;

namespace XywireHost.Core.services;

public class PluginService
{
    private const string PluginPathsKey = "plugin_paths";
    private List<string> _pluginPaths = [];

    public IReadOnlyList<string> PluginPaths => _pluginPaths.AsReadOnly();

    public event Action? PluginPathsChanged;

    public PluginService()
    {
        LoadPluginPaths();
    }

    public void AddPluginPath(string path)
    {
        if (!_pluginPaths.Contains(path))
        {
            _pluginPaths.Add(path);
            SavePluginPaths();
            PluginPathsChanged?.Invoke();
        }
    }

    public void RemovePluginPath(string path)
    {
        if (_pluginPaths.Remove(path))
        {
            SavePluginPaths();
            PluginPathsChanged?.Invoke();
        }
    }

    public void ClearPluginPaths()
    {
        _pluginPaths.Clear();
        SavePluginPaths();
        PluginPathsChanged?.Invoke();
    }

    private void LoadPluginPaths()
    {
        try
        {
            string? json = GetStoredValue(PluginPathsKey);
            if (!string.IsNullOrEmpty(json))
            {
                _pluginPaths = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
        }
        catch
        {
            _pluginPaths = [];
        }
    }

    private void SavePluginPaths()
    {
        try
        {
            string json = JsonSerializer.Serialize(_pluginPaths);
            SetStoredValue(PluginPathsKey, json);
        }
        catch
        {
            // Silently fail if save doesn't work
        }
    }

    // These methods will be implemented differently based on platform
    // For now, we'll use a simple in-memory approach that can be overridden
    protected virtual string? GetStoredValue(string key)
    {
        // This will be replaced by platform-specific storage in the UI layer
        return null;
    }

    protected virtual void SetStoredValue(string key, string value)
    {
        // This will be replaced by platform-specific storage in the UI layer
    }
}
