using System.Net;
using System.Net.Sockets;

namespace XywireHost.Core.core;

public class LedLine : IDisposable
{
    private const byte BrightnessPacketId = 1;
    private const byte DataPacketId = 2;
    private const byte ClearPacketId = 3;

    private const int DeadInFront = 1;

    private readonly byte[] _dataPacketBuffer;
    private readonly byte[] _clearPacketBuffer = [ClearPacketId];
    private readonly byte[] _brightnessPacketBuffer = [BrightnessPacketId, 0];

    private readonly IPEndPoint _ipEndPoint;
    private readonly UdpClient _udpClient;
    private readonly List<TaskCompletionSource<byte[]>> _pendingReceives = [];

    private readonly Thread _udpListenerThread;

    private uint _generation = 1;

    private SocketException? _socketException = null;

    public LedLine(string ipAddress, int port = 25565, int width = 14, int height = 14)
    {
        _ipEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
        _udpClient = new UdpClient();
        _udpClient.Connect(_ipEndPoint);

        Width = width;
        Height = height;
        LedsCount = width * height;

        // 1 byte for type 4 bytes for generation + (3 * NUM_LEDS) max data
        _dataPacketBuffer = new byte[1 + 4 + (DeadInFront + LedsCount) * 3];
        _dataPacketBuffer[0] = DataPacketId;

        _udpListenerThread = new Thread(UdpReadingLoop);
        _udpListenerThread.Start();
    }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public int LedsCount { get; }

    public void Dispose() => _udpClient.Dispose();

    public void SetColors(Color[][] colors)
    {
        int n = colors.Length;
        int m = colors[0].Length;

        for (int i = 0; i < LedsCount; i++)
        {
            int column = i / n;
            int row = column % 2 == 0 ? i % n : n - 1 - i % n;
            // ColorToData(colors[n - 1 - row][m - 1 - column], i * 3);
            ColorToData(colors[n - 1 - row][column], i * 3);
        }

        SendDataPacket();
    }

    public void SetColors(Color[,] colors)
    {
        int n = colors.GetLength(0);
        int m = colors.GetLength(1);

        for (int i = 0; i < LedsCount; i++)
        {
            int column = i / n;
            int row = column % 2 == 0 ? i % n : n - 1 - i % n;
            // ColorToData(colors[n - 1 - row, m - 1 - column], i * 3);
            ColorToData(colors[n - 1 - row, column], i * 3);
        }

        SendDataPacket();
    }

    public void SetColors(Color[] colors)
    {
        for (int i = 0; i < LedsCount; i++)
        {
            ColorToData(colors[i], i * 3);
        }

        SendDataPacket();
    }

    private void ColorToData(Color color, int shift)
    {
        SetDataByte(color.Red, shift);
        SetDataByte(color.Green, shift + 1);
        SetDataByte(color.Blue, shift + 2);
    }

    private void SetDataByte(byte value, int at)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(at);
        _dataPacketBuffer[at + 5 + DeadInFront * 3] = value;
    }

    public async Task SendBrightnessPacket(byte brightness)
    {
        _brightnessPacketBuffer[1] = brightness;

        await SendAcked(_brightnessPacketBuffer);
    }

    private void SendDataPacket()
    {
        GenerationBytesToArray(_generation, _dataPacketBuffer, 1);

        _udpClient.Send(_dataPacketBuffer);
        _generation++;
    }

    public Task SendClearPacket() => SendAcked(_clearPacketBuffer);

    private async Task SendAcked(byte[] packet)
    {
        while (_socketException == null)
        {
            TaskCompletionSource<byte[]> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_pendingReceives)
            {
                _pendingReceives.Add(tcs);
            }

            await _udpClient.SendAsync(packet);

            Task completed = await Task.WhenAny(tcs.Task, Task.Delay(200));

            if (completed == tcs.Task) return;

            lock (_pendingReceives)
            {
                _pendingReceives.Remove(tcs);
            }
        }

        throw _socketException;
    }

    private void UdpReadingLoop()
    {
        while (true)
        {
            try
            {
                IPEndPoint remoteEndPoint = new(IPAddress.Any, 0);
                byte[] received = _udpClient.Receive(ref remoteEndPoint);

                lock (_pendingReceives)
                {
                    if (_pendingReceives.Count == 0) continue;

                    TaskCompletionSource<byte[]> tcs = _pendingReceives.Last();
                    _pendingReceives.RemoveAt(_pendingReceives.Count - 1);
                    tcs.SetResult(received);
                }
            }
            catch (SocketException e)
            {
                _socketException = e;
                break;
            }
        }
    }

    private static void GenerationBytesToArray(uint generation, byte[] array, int startIndex)
    {
        array[startIndex] = (byte)(generation & 0xFFu);
        generation >>= 8;
        array[startIndex + 1] = (byte)(generation & 0xFFu);
        generation >>= 8;
        array[startIndex + 2] = (byte)(generation & 0xFFu);
        generation >>= 8;
        array[startIndex + 3] = (byte)(generation & 0xFFu);
    }
}
