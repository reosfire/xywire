using Leds.core;
using Leds.effects;
using System.Text;
using Leds.effects._1d;
using Leds.effects._2d;
using Leds.effects.tests;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using System.Text.Json;

Console.WriteLine("Press 's' to start WiFi setup process. Or any other key to connect via WiFi.");
var DeviceStorePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "XywireHost",
    "devices.json");

var key = Console.ReadKey(true);
if (key.Key == ConsoleKey.S)
{
    Console.WriteLine("Starting WiFi setup...");
    SetupWiFiCredentials();
}
else
{
    var device = SelectDevice();
    Console.WriteLine($"Connecting to device at {device}...");
    StartDeviceConnection(device);
}

return;

void SetupWiFiCredentials()
{
    using var client = new BluetoothClient();

    Console.WriteLine("Searching for Bluetooth devices, please wait...");
    var devices = client.DiscoverDevices().ToList();

    if (devices.Count == 0)
    {
        Console.WriteLine("No Bluetooth devices found.");
        return;
    }

    for (var i = 0; i < devices.Count; i++)
    {
        Console.WriteLine($"{i}: {devices[i].DeviceName} [{devices[i].DeviceAddress}]");
    }

    Console.Write("\nSelect device number: ");
    if (!int.TryParse(Console.ReadLine(), out var choice) ||
        choice < 0 || choice >= devices.Count)
    {
        Console.WriteLine("Invalid selection.");
        return;
    }

    var selectedDevice = devices[choice];
    Console.WriteLine($"Connecting to {selectedDevice.DeviceName}...");
    
    client.Connect(selectedDevice.DeviceAddress, BluetoothService.SerialPort);

    using Stream stream = client.GetStream();

    Console.WriteLine("Enter SSID:");
    var ssid = Console.ReadLine();
    
    Console.WriteLine("Enter Password:");
    var password = Console.ReadLine();

    var data = Encoding.ASCII.GetBytes($"{ssid}\0{password}\0");
    var packet = new byte[data.Length + 1];
    packet[0] = 4;
    Array.Copy(data, 0, packet, 1, data.Length);

    stream.Write(packet, 0, packet.Length);
    stream.Flush();

    Console.WriteLine("Wifi credentials sent successfully.");
}

string SelectDevice()
{
    var devices = LoadSavedDevices();

    while (true)
    {
        if (devices.Count == 0)
        {
            Console.WriteLine("No saved devices yet.");
            var newDevice = PromptForDevice();
            devices.Add(newDevice);
            SaveDevices(devices);
            return newDevice;
        }

        Console.WriteLine("Saved devices:");
        for (var i = 0; i < devices.Count; i++)
        {
            Console.WriteLine($"{i}: {devices[i]}");
        }

        Console.Write("Select device number or type 'a' to add new: ");
        var input = Console.ReadLine()?.Trim();

        if (string.Equals(input, "a", StringComparison.OrdinalIgnoreCase))
        {
            var newDevice = PromptForDevice();
            devices.Add(newDevice);
            SaveDevices(devices);
            return newDevice;
        }

        if (int.TryParse(input, out var index) && index >= 0 && index < devices.Count)
        {
            return devices[index];
        }

        Console.WriteLine("Invalid selection.");
    }
}

string PromptForDevice()
{
    Console.Write("Enter device IP or hostname: ");
    var address = Console.ReadLine()?.Trim() ?? string.Empty;

    while (string.IsNullOrWhiteSpace(address))
    {
        Console.Write("Address cannot be empty. Enter device IP or hostname: ");
        address = Console.ReadLine()?.Trim() ?? string.Empty;
    }

    return address;
}

List<string> LoadSavedDevices()
{
    try
    {
        if (!File.Exists(DeviceStorePath))
        {
            return [];
        }

        var json = File.ReadAllText(DeviceStorePath);
        var devices = JsonSerializer.Deserialize<List<string>>(json);
        return devices ?? [];
    }
    catch
    {
        Console.WriteLine("Warning: failed to load saved devices; starting fresh.");
        return [];
    }
}

void SaveDevices(List<string> devices)
{
    try
    {
        var folder = Path.GetDirectoryName(DeviceStorePath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var json = JsonSerializer.Serialize(devices, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(DeviceStorePath, json);
    }
    catch
    {
        Console.WriteLine("Warning: failed to save devices.");
    }
}

void StartDeviceConnection(string ipAddress)
{
    EffectFactory[] effectsRegistry =
    [
        new("Coordinate System Test", (line) => new CoordinateSystemTest(line)),
        new("Max fps test", (line) => new MaxFpsTest(line)),
        new("Beautiful bugged fft", (line) => new BuggedBeautifulFurrier(line)),
        new("Beautiful bugged fft. One line", (line) => new StraightBuggedBeautifulFurrier(line)),
        new("Beautiful bugged fft. One line MIC", (line) => new StraightBuggedBeautifulFurrierMic(line)),
        new("Tests fft", (line) => new FurrierTests(line)),
        new("Rainbow", (line) => new Rainbow(line)),
        new("Rain", (line) => new Rain(line)),
        new("Tree", (line) => new Tree(line)),
        new("Tests", (line) => new Tests(line)),

        new("Rainbow wave", (line) => new RainbowWave(line)),
        new("Sparcles", (line) => new Sparkles(line)),
        new("Comet", (line) => new Comet(line)),
        new("Dual Comet", (line) => new DualComet(line)),
        new("Fireworks", (line) => new Fireworks(line)),
        new("Color pulse", (line) => new ColorPulse(line)),
        new("Wave ripple", (line) => new WaveRipple(line)),
        new("Twinkle stars", (line) => new TwinkleStars(line)),
        new("Equalizer bars", (line) => new EqualizerBars(line)),
        new("SpectrumWaves", (line) => new SpectrumWaves(line)),
        new("DynamicPulse", (line) => new DynamicPulse(line)),
        new("GameOfLife1D", (line) => new GameOfLife1D(line)),
        new("Game of Life", (line) => new GameOfLife(line)),
        new("Snake Game", (line) => new SnakeGame(line)),
        new("Rotating Cube", (line) => new RotatingCube(line)),
        new("Julia Set Zoom", (line) => new JuliaSetZoom(line)),
        new("Flashing Letters Text", (line) => new FlashingLettersText(line)),
        new("Self Playing Snake", (line) => new SelfPlayingSnake(line)),
    ];

    LedLine ledLine = new(ipAddress: ipAddress, brightness: 100);

    AbstractEffect? currentEffect = null;

    while (true)
    {
        for (var i = 0; i < effectsRegistry.Length; i++)
        {
            Console.WriteLine($"{i}   {effectsRegistry[i].Name}");
        }

        var effectInput = Console.ReadLine();

        if (!int.TryParse(effectInput, out var effectNumber)) continue;
        if (effectNumber < 0 || effectNumber > effectsRegistry.Length) continue;

        currentEffect?.StopLooping();

        currentEffect = effectsRegistry[effectNumber].Factory(ledLine);

        currentEffect.StartLooping();
    }
}
