using System.Text;
using InTheHand.Net.Sockets;
using BtService = InTheHand.Net.Bluetooth.BluetoothService;

namespace XywireHost.Core.services;

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
            using BluetoothClient client = new();
            IReadOnlyCollection<BluetoothDeviceInfo>? devices = client.DiscoverDevices();
            return devices.Select(d => new BluetoothDeviceDto
            {
                DeviceName = d.DeviceName, DeviceAddress = d.DeviceAddress.ToString(), InternalDevice = d,
            }).ToList();
        });
    }

    public async Task SendWiFiCredentialsAsync(BluetoothDeviceDto device, string ssid, string password)
    {
        if (device.InternalDevice == null)
            throw new ArgumentException("Invalid device");

        await Task.Run(() =>
        {
            using BluetoothClient client = new();
            client.Connect(device.InternalDevice.DeviceAddress, BtService.SerialPort);

            using Stream stream = client.GetStream();

            byte[] data = Encoding.ASCII.GetBytes($"{ssid}\0{password}\0");
            byte[] packet = new byte[data.Length + 1];
            packet[0] = 4;
            Array.Copy(data, 0, packet, 1, data.Length);

            stream.Write(packet, 0, packet.Length);
            stream.Flush();
        });
    }
}
