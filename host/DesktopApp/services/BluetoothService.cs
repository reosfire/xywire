using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using System.Text;
using BtService = InTheHand.Net.Bluetooth.BluetoothService;

namespace Leds.services;

public class BluetoothDeviceDto
{
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceAddress { get; set; } = string.Empty;
    internal BluetoothDeviceInfo? InternalDevice { get; set; }
}

public class BluetoothService
{
    public async Task<List<BluetoothDeviceDto>> DiscoverDevicesAsync()
    {
        return await Task.Run(() =>
        {
            using var client = new BluetoothClient();
            var devices = client.DiscoverDevices();
            return devices.Select(d => new BluetoothDeviceDto
            {
                DeviceName = d.DeviceName,
                DeviceAddress = d.DeviceAddress.ToString(),
                InternalDevice = d
            }).ToList();
        });
    }

    public async Task SendWiFiCredentialsAsync(BluetoothDeviceDto device, string ssid, string password)
    {
        if (device.InternalDevice == null)
            throw new ArgumentException("Invalid device");

        await Task.Run(() =>
        {
            using var client = new BluetoothClient();
            client.Connect(device.InternalDevice.DeviceAddress, BtService.SerialPort);

            using Stream stream = client.GetStream();

            var data = Encoding.ASCII.GetBytes($"{ssid}\0{password}\0");
            var packet = new byte[data.Length + 1];
            packet[0] = 4;
            Array.Copy(data, 0, packet, 1, data.Length);

            stream.Write(packet, 0, packet.Length);
            stream.Flush();
        });
    }
}
