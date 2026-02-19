using XywireHost.Core.services;

namespace XywireHost.UI.Services;

public class MauiPluginService : PluginService
{
    protected override string? GetStoredValue(string key)
    {
        return Preferences.Get(key, null);
    }

    protected override void SetStoredValue(string key, string value)
    {
        Preferences.Set(key, value);
    }
}
