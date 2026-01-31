using System.Text.Json;

namespace Leds.services;

public class DeviceService
{
    private readonly string _deviceStorePath;

    public DeviceService()
    {
        _deviceStorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XywireHost",
            "devices.json");
    }

    public List<string> LoadSavedDevices()
    {
        try
        {
            if (!File.Exists(_deviceStorePath))
            {
                return [];
            }

            var json = File.ReadAllText(_deviceStorePath);
            var devices = JsonSerializer.Deserialize<List<string>>(json);
            return devices ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void SaveDevices(List<string> devices)
    {
        try
        {
            var folder = Path.GetDirectoryName(_deviceStorePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var json = JsonSerializer.Serialize(devices, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_deviceStorePath, json);
        }
        catch
        {
            throw new Exception("Failed to save devices.");
        }
    }

    public void AddDevice(string deviceAddress)
    {
        var devices = LoadSavedDevices();
        if (!devices.Contains(deviceAddress))
        {
            devices.Add(deviceAddress);
            SaveDevices(devices);
        }
    }

    public void RemoveDevice(string deviceAddress)
    {
        var devices = LoadSavedDevices();
        devices.Remove(deviceAddress);
        SaveDevices(devices);
    }
}
