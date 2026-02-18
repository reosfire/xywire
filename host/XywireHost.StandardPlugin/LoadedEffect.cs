using XywireHost.Core;
using XywireHost.Core.core;

namespace XywireHost.StandardPlugin;

internal class ConstantEffect<T> : BaseEffect
{
    private readonly OutputSlot<T> _outputSlot;

    private T? _value;
    private bool _effectInitialized = false;

    public ConstantEffect()
    {
        _outputSlot = OutputSlots.RegisterOutput<T>("value");
        EmbeddedInputHandles.RegisterInput<T>("value", SetValue);
    }

    public override void Initialize(IEffectContext context)
    {
        _effectInitialized = true;

        if (_value != null)
        {
            _outputSlot.Invoke(_value);
        }
    }

    private void SetValue(T value)
    {
        _value = value;

        if (_effectInitialized)
        {
            _outputSlot.Invoke(_value);
        }
    }
}

internal class RainbowEffect : BaseEffect
{
    private readonly OutputSlot<IReadOnlyBuffer2D<Color>> _colorsOutput;
    private Buffer2D<Color> _colorBuffer;

    private IEffectContext? _context;
    private TaskHandle? _renderTaskHandle;

    private int _width;
    private int _height;
    private int _fps;

    private int _counter;

    public RainbowEffect()
    {
        _colorsOutput = OutputSlots.RegisterOutput<IReadOnlyBuffer2D<Color>>("colorBuffer");

        InputHandles.RegisterInput<int>("width", SetWidth);
        InputHandles.RegisterInput<int>("height", SetHeight);
        InputHandles.RegisterInput<int>("fps", SetFps);
    }

    public override void Initialize(IEffectContext context)
    {
        _context = context;
        Restart();
    }

    private void Render()
    {
        _colorBuffer.ShiftDown();

        double stepHueOffset = 360.0 / _height;

        for (int i = 0; i < _height; i++)
        {
            _colorBuffer[new Index2D(0, i)] = Color.HSV(_counter * stepHueOffset % 360, 1, 1);
        }

        _counter++;

        _colorsOutput.Invoke(_colorBuffer);
    }

    private void Restart()
    {
        _renderTaskHandle?.Stop();

        if (_width <= 0 || _height <= 0 || _fps <= 0) return;
        if (_context == null) return;

        _colorBuffer = new Buffer2D<Color>(_height, _width);
        _renderTaskHandle = _context!.Scheduler.ScheduleTask(Render, _fps);
    }

    private void SetWidth(int width)
    {
        _width = width;
        Restart();
    }

    private void SetHeight(int height)
    {
        _height = height;
        Restart();
    }

    private void SetFps(int fps)
    {
        _fps = fps;
        Restart();
    }
}

internal class OverlayEffect : BaseEffect
{
    private readonly OutputSlot<IReadOnlyBuffer2D<Color>> _colorsOutput;

    private IReadOnlyBuffer2D<Color>? _buffer0;
    private IReadOnlyBuffer2D<Color>? _buffer1;

    public OverlayEffect()
    {
        _colorsOutput = OutputSlots.RegisterOutput<IReadOnlyBuffer2D<Color>>("colorBuffer");

        InputHandles.RegisterInput<IReadOnlyBuffer2D<Color>>("colorBuffer0", SetColorBuffer0);
        InputHandles.RegisterInput<IReadOnlyBuffer2D<Color>>("colorBuffer1", SetColorBuffer1);
    }

    private void SetColorBuffer0(IReadOnlyBuffer2D<Color> buffer)
    {
        _buffer0 = buffer;
        BlendAndOutput();
    }

    private void SetColorBuffer1(IReadOnlyBuffer2D<Color> buffer)
    {
        _buffer1 = buffer;
        BlendAndOutput();
    }

    private void BlendAndOutput()
    {
        if (_buffer0 == null || _buffer1 == null) return;

        // Use buffer0 dimensions as base
        int rows = _buffer0.Rows;
        int cols = _buffer0.Cols;

        Buffer2D<Color> result = new(rows, cols);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                Index2D idx = new(row, col);
                Color c0 = _buffer0[idx];

                // If buffer1 has this pixel, blend; otherwise use buffer0
                if (row < _buffer1.Rows && col < _buffer1.Cols)
                {
                    Color c1 = _buffer1[new Index2D(row, col)];
                    // Additive blend (clamped to 255)
                    byte r = (byte)Math.Min(255, c0.Red + c1.Red);
                    byte g = (byte)Math.Min(255, c0.Green + c1.Green);
                    byte b = (byte)Math.Min(255, c0.Blue + c1.Blue);
                    result[idx] = Color.RGB(r, g, b);
                }
                else
                {
                    result[idx] = c0;
                }
            }
        }

        _colorsOutput.Invoke(result);
    }
}

internal class MulticastEffect<T> : BaseEffect
{
    private readonly OutputSlot<T> _colorsOutput0;
    private readonly OutputSlot<T> _colorsOutput1;

    public MulticastEffect()
    {
        _colorsOutput0 = OutputSlots.RegisterOutput<T>("output0");
        _colorsOutput1 = OutputSlots.RegisterOutput<T>("output1");

        InputHandles.RegisterInput<T>("colorBuffer", SetColorBuffer);
    }

    private void SetColorBuffer(T buffer)
    {
        _colorsOutput0.Invoke(buffer);
        _colorsOutput1.Invoke(buffer);
    }
}

internal class WhiteCircleEffect : BaseEffect
{
    private readonly OutputSlot<IReadOnlyBuffer2D<Color>> _colorsOutput;
    private Buffer2D<Color> _colorBuffer;

    private IEffectContext? _context;
    private TaskHandle? _renderTaskHandle;

    private int _width;
    private int _height;
    private int _fps;
    private double _radius;
    
    // SSAA sample count per axis (total samples = _ssaaSamples^2)
    private const int SsaaSamples = 4;

    public WhiteCircleEffect()
    {
        _colorsOutput = OutputSlots.RegisterOutput<IReadOnlyBuffer2D<Color>>("colorBuffer");

        InputHandles.RegisterInput<int>("width", SetWidth);
        InputHandles.RegisterInput<int>("height", SetHeight);
        InputHandles.RegisterInput<int>("fps", SetFps);
        InputHandles.RegisterInput<double>("radius", SetRadius);
    }

    public override void Initialize(IEffectContext context)
    {
        _context = context;
        Restart();
    }

    private void Render()
    {
        double centerRow = _height / 2.0;
        double centerCol = _width / 2.0;
        
        double sampleStep = 1.0 / SsaaSamples;
        double sampleOffset = sampleStep / 2.0;
        int totalSamples = SsaaSamples * SsaaSamples;
        
        for (int row = 0; row < _height; row++)
        {
            for (int col = 0; col < _width; col++)
            {
                int insideCount = 0;
                
                // Sample grid within pixel for SSAA
                for (int sy = 0; sy < SsaaSamples; sy++)
                {
                    for (int sx = 0; sx < SsaaSamples; sx++)
                    {
                        double sampleX = col + sampleOffset + sx * sampleStep;
                        double sampleY = row + sampleOffset + sy * sampleStep;
                        
                        double dx = sampleX - centerCol;
                        double dy = sampleY - centerRow;
                        double distance = Math.Sqrt(dx * dx + dy * dy);
                        
                        // SDF: negative inside circle, positive outside
                        double sdfValue = distance - _radius;
                        
                        if (sdfValue < 0)
                        {
                            insideCount++;
                        }
                    }
                }
                
                // Calculate coverage ratio for anti-aliasing
                double coverage = (double)insideCount / totalSamples;
                byte intensity = (byte)(255 * coverage);
                
                _colorBuffer[new Index2D(row, col)] = Color.RGB(intensity, intensity, intensity);
            }
        }

        _colorsOutput.Invoke(_colorBuffer);
    }

    private void Restart()
    {
        _renderTaskHandle?.Stop();

        if (_width <= 0 || _height <= 0 || _fps <= 0) return;
        if (_context == null) return;

        _colorBuffer = new Buffer2D<Color>(_height, _width);
        _renderTaskHandle = _context!.Scheduler.ScheduleTask(Render, _fps);
    }

    private void SetWidth(int width)
    {
        _width = width;
        Restart();
    }

    private void SetHeight(int height)
    {
        _height = height;
        Restart();
    }

    private void SetFps(int fps)
    {
        _fps = fps;
        Restart();
    }

    private void SetRadius(double radius)
    {
        _radius = radius;
    }
}

internal class SelectEffect : BaseEffect
{
    private readonly OutputSlot<IReadOnlyBuffer2D<Color>> _colorsOutput;

    public SelectEffect()
    {
        _colorsOutput = OutputSlots.RegisterOutput<IReadOnlyBuffer2D<Color>>("colorBuffer");

        InputHandles.RegisterInput<IReadOnlyBuffer2D<Color>>("colorBuffer0", SetColorBuffer0);
        InputHandles.RegisterInput<IReadOnlyBuffer2D<Color>>("colorBuffer1", SetColorBuffer1);
    }

    private void SetColorBuffer0(IReadOnlyBuffer2D<Color> buffer) => _colorsOutput.Invoke(buffer);

    private void SetColorBuffer1(IReadOnlyBuffer2D<Color> buffer) => _colorsOutput.Invoke(buffer);
}

public sealed class LedLineEffect : BaseEffect
{
    private LedLine? _ledLine;

    public LedLineEffect()
    {
        // TODO sort input output registration across project.
        EmbeddedInputHandles.RegisterInput<LedLine>("ledLine", SetLedLine);
        InputHandles.RegisterInput<IReadOnlyBuffer2D<Color>>("colorBuffer", SetColorBuffer);
    }

    private void SetLedLine(LedLine value)
    {
        // TODO this shit is async and it's maybe weird to clear led line here.
        // but it's required because on real esp there is a generation int which must be cleared before starting transmission
        value.SendClearPacket();
        _ledLine = value;
    }

    // TODO actually it should accept 1D buffer
    private void SetColorBuffer(IReadOnlyBuffer2D<Color> buffer)
    {
        if (_ledLine == null) return;

        Color[][] colors = new Color[buffer.Rows][];
        for (int row = 0; row < buffer.Rows; row++)
        {
            colors[row] = new Color[buffer.Cols];
            for (int col = 0; col < buffer.Cols; col++)
            {
                colors[row][col] = buffer[new Index2D(row, col)];
            }
        }

        _ledLine.SetColors(colors);
    }
}

internal class TickerEffect : BaseEffect
{
    private readonly OutputSlot<int> _frameOutput;

    private IEffectContext? _context;
    private TaskHandle? _renderTaskHandle;

    private int _fps;
    private int _frameCounter;

    public TickerEffect()
    {
        _frameOutput = OutputSlots.RegisterOutput<int>("frame");
        InputHandles.RegisterInput<int>("fps", SetFps);
    }

    public override void Initialize(IEffectContext context)
    {
        _context = context;
        Restart();
    }

    private void Tick()
    {
        _frameOutput.Invoke(_frameCounter);
        _frameCounter++;
    }

    private void Restart()
    {
        _renderTaskHandle?.Stop();

        if (_fps <= 0) return;
        if (_context == null) return;

        _frameCounter = 0;
        _renderTaskHandle = _context.Scheduler.ScheduleTask(Tick, _fps);
    }

    private void SetFps(int fps)
    {
        _fps = fps;
        Restart();
    }
}

internal class SinEffect : BaseEffect
{
    private readonly OutputSlot<double> _output;

    public SinEffect()
    {
        _output = OutputSlots.RegisterOutput<double>("result");
        InputHandles.RegisterInput<double>("value", SetValue);
    }

    private void SetValue(double value)
    {
        _output.Invoke(Math.Sin(value));
    }
}

internal class MultiplyEffect : BaseEffect
{
    private readonly OutputSlot<double> _output;

    private double? _a;
    private double? _b;

    public MultiplyEffect()
    {
        _output = OutputSlots.RegisterOutput<double>("result");
        InputHandles.RegisterInput<double>("a", SetA);
        InputHandles.RegisterInput<double>("b", SetB);
    }

    private void SetA(double value)
    {
        _a = value;
        TryCompute();
    }

    private void SetB(double value)
    {
        _b = value;
        TryCompute();
    }

    private void TryCompute()
    {
        if (_a.HasValue && _b.HasValue)
        {
            _output.Invoke(_a.Value * _b.Value);
        }
    }
}

internal class IntToDoubleEffect : BaseEffect
{
    private readonly OutputSlot<double> _output;

    public IntToDoubleEffect()
    {
        _output = OutputSlots.RegisterOutput<double>("result");
        InputHandles.RegisterInput<int>("value", SetValue);
    }

    private void SetValue(int value)
    {
        _output.Invoke(value);
    }
}

internal class DoubleToIntEffect : BaseEffect
{
    private readonly OutputSlot<int> _output;

    public DoubleToIntEffect()
    {
        _output = OutputSlots.RegisterOutput<int>("result");
        InputHandles.RegisterInput<double>("value", SetValue);
    }

    private void SetValue(double value)
    {
        _output.Invoke((int)value);
    }
}

internal class AddEffect : BaseEffect
{
    private readonly OutputSlot<double> _output;

    private double? _a;
    private double? _b;

    public AddEffect()
    {
        _output = OutputSlots.RegisterOutput<double>("result");
        InputHandles.RegisterInput<double>("a", SetA);
        InputHandles.RegisterInput<double>("b", SetB);
    }

    private void SetA(double value)
    {
        _a = value;
        TryCompute();
    }

    private void SetB(double value)
    {
        _b = value;
        TryCompute();
    }

    private void TryCompute()
    {
        if (_a.HasValue && _b.HasValue)
        {
            _output.Invoke(_a.Value + _b.Value);
        }
    }
}
