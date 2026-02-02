using System.Text.Json;

namespace XywireHost.Core.services;

public class DeviceService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _deviceStorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XywireHost",
        "devices.json");

    public List<string> LoadSavedDevices()
    {
        try
        {
            if (!File.Exists(_deviceStorePath))
            {
                return [];
            }

            string json = File.ReadAllText(_deviceStorePath);
            List<string>? devices = JsonSerializer.Deserialize<List<string>>(json);
            return devices ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveDevices(List<string> devices)
    {
        try
        {
            string? folder = Path.GetDirectoryName(_deviceStorePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string json = JsonSerializer.Serialize(devices, SerializerOptions);
            File.WriteAllText(_deviceStorePath, json);
        }
        catch
        {
            throw new Exception("Failed to save devices.");
        }
    }

    public void AddDevice(string deviceAddress)
    {
        List<string> devices = LoadSavedDevices();
        if (devices.Contains(deviceAddress)) return;

        devices.Add(deviceAddress);
        SaveDevices(devices);
    }

    public void RemoveDevice(string deviceAddress)
    {
        List<string> devices = LoadSavedDevices();
        devices.Remove(deviceAddress);
        SaveDevices(devices);
    }
}
