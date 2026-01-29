using System.Net;
using System.Net.Sockets;

namespace Leds.core
{
    public class LedLine
    {
        private const byte BrightnessPacketId = 1;
        private const byte DataPacketId = 2;
        private const byte ClearPacketId = 3;

        private byte _brightness;
        private readonly UdpClient _udpClient;

        private readonly byte[] _dataPacketBuffer;
        
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int LedsCount { get; }

        public byte Brightness
        {
            get => _brightness;
            set
            {
                SendBrightnessPacket(value);
                _brightness = value;
            }
        }

        private int deadInFront = 1;

        public LedLine(string ipAddress, int port = 25565, int width = 14, int height = 14, byte brightness = 20)
        {
            _udpClient = new UdpClient();
            _udpClient.Connect(IPAddress.Parse(ipAddress), port);

            Clear();
            Width = width;
            Height = height;
            LedsCount = width * height;

            _dataPacketBuffer = new byte[1 + 4 + (deadInFront + LedsCount) * 3]; // 1 byte for type 4 bytes for generation + (3 * NUM_LEDS) max data
            _dataPacketBuffer[0] = DataPacketId;

            Brightness = brightness;
        }

        public void SetColors(Color[][] colors)
        {
            int n = colors.Length;
            int m = colors[0].Length;

            for (int i = 0; i < LedsCount; i++)
            {
                int column = i / n;
                int row = (column % 2 == 0) ? i % n : n - 1 - i % n;
                // ColorToData(colors[n - 1 - row][m - 1 - column], i * 3);
                ColorToData(colors[n - 1 - row][column], i * 3);
            }

            SendDataPacket();
        }
        
        public void SetColors(Color[,] colors)
        {
            int n = colors.GetLength(0);

            for (int i = 0; i < LedsCount; i++)
            {
                int column = i / n;
                int row = (column % 2 == 0) ? i % n : n - 1 - i % n;
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

        public void Clear()
        {
            SendClearPacket();
        }

        private void ColorToData(Color color, int shift)
        {
            SetDataByte(color.Red, shift);
            SetDataByte(color.Green, shift + 1);
            SetDataByte(color.Blue, shift + 2);
        }

        private void SetDataByte(byte value, int at)
        {
            if (at < 0) throw new ArgumentOutOfRangeException(nameof(at));
            _dataPacketBuffer[at + 5 + deadInFront * 3] = value;
        }

        private void SendBrightnessPacket(byte brightness)
        {
            var packet = new byte[] { BrightnessPacketId, brightness };

            while (true)
            {
                _udpClient.Send(packet);
                var receiveResult = _udpClient.ReceiveAsync().Wait(100);
                if (receiveResult) break;
            }
        }

        private uint generation = 1;
        private void SendDataPacket()
        {
            var generationBytes = GenerationBytes(generation);
            Array.Copy(generationBytes, 0, _dataPacketBuffer, 1, generationBytes.Length);

            _udpClient.Send(_dataPacketBuffer);
            generation++;
        }

        private void SendClearPacket()
        {
            var packet = new byte[] { ClearPacketId };

            while (true)
            {
                _udpClient.Send(packet);
                var receiveResult = _udpClient.ReceiveAsync().Wait(100);
                if (receiveResult) break;
            }
        }

        private byte[] GenerationBytes(uint generation)
        {
            byte[] result = new byte[4];

            result[0] = (byte)(generation & 0xFFu);
            generation >>= 8;
            result[1] = (byte)(generation & 0xFFu);
            generation >>= 8;
            result[2] = (byte)(generation & 0xFFu);
            generation >>= 8;
            result[3] = (byte)(generation & 0xFFu);

            return result;
        }
    }
}
