using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using XywireHost.Core.core;

namespace ExamplePlugin;

public class LoadedEffect : LoadedEffectBase
{
    private int _currentFrame = 0;

    public override void FillFrame(Color[][] buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            for (int j = 0; j < buffer.Length; j++)
            {
                int red = (_currentFrame + i * 10) % 256;
                int green = (_currentFrame + j * 10) % 256;
                int blue = _currentFrame % 256;
                buffer[i][j] = Color.RGB(red, green, blue);
            }
        }

        _currentFrame++;
    }
}

interface IDataSink
{
    IEffectInputsCollection InputHandles { get; }
}

interface IDataSource
{
    IEffectOutputsCollection OutputSlots { get; }
}

class BaseEffect : IDataSource, IDataSink
{
    private readonly EffectOutputsCollection _outputs = new();
    private readonly EffectInputsCollection _inputs = new();

    public IEffectOutputsCollection OutputSlots => _outputs;
    public IEffectInputsCollection InputHandles => _inputs;
}

class LedLine : IDataSink
{
    public IEffectInputsCollection InputHandles { get; }
}

interface IUntypedOutputSlot
{
}

interface IUntypedInputHandle
{
}

class OutputSlot<T>(string id, Action<T>? setValue) : IUntypedOutputSlot
{
    private string Id { get; } = id;
    private Action<T>? SetValue { get; } = setValue;

    public static implicit operator Action<T>(OutputSlot<T> outputSlot)
    {
        if (outputSlot.SetValue == null)
            throw new InvalidOperationException($"Output slot {outputSlot.Id} is not connected to any input.");
        return outputSlot.SetValue!;
    }
}

class InputHandle<T>(string id, Action<T> setValue) : IUntypedInputHandle
{
    private string Id { get; } = id;
    private Action<T> SetValue { get; } = setValue;
}

interface IEffectInputsCollection
{
    void RegisterInput<T>(string name, Action<T> callback);
}

interface IEffectOutputsCollection
{
    OutputSlot<T> RegisterOutput<T>(string name);
}

class EffectInputsCollection : IEffectInputsCollection
{
    private Dictionary<string, IUntypedInputHandle> _inputs = new();

    public void RegisterInput<T>(string name, Action<T> callback)
    {
        _inputs[name] = new InputHandle<T>(name, callback);
    }
}

class EffectOutputsCollection : IEffectOutputsCollection
{
    private Dictionary<string, IUntypedOutputSlot> _outputs = new();

    public OutputSlot<T> RegisterOutput<T>(string name)
    {
        var outputSlot = new OutputSlot<T>(name, null);
        _outputs[name] = outputSlot;
        return
    }
}

readonly record struct Index2D(int Row, int Col);

interface IReadOnlyBuffer2D<T>
{
    T this[Index2D index] { get; }
}

readonly struct Buffer2D<T> : IReadOnlyBuffer2D<T>
{
    private readonly T[,] _data;
    
    public Buffer2D(int rows, int cols)
    {
        _data = new T[rows, cols];
    }

    public T this[Index2D index]
    {
        get => _data[index.Row, index.Col];
        set => _data[index.Row, index.Col] = value;
    }
}

class RainbowEffect : BaseEffect
{
    private Buffer2D<Color> _colorBuffer;

    private readonly OutputSlot<IReadOnlyBuffer2D<Color>> _colorsOutput;

    private int _width;
    private int _height;
    private int _fps;

    public RainbowEffect()
    {
        _colorsOutput = OutputSlots.RegisterOutput<IReadOnlyBuffer2D<Color>>("colorBuffer");

        InputHandles.RegisterInput<int>("width", SetWidth);
        InputHandles.RegisterInput<int>("height", SetHeight);
        InputHandles.RegisterInput<int>("fps", SetFps);
    }

    private void Render()
    { 
        _colorsOutput(_colorBuffer);
    }

    private void Restart()
    {
        _colorBuffer = new Buffer2D<Color>(_height, _width);
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

class CubeEffect : BaseEffect
{
}

class OverlayEffect : BaseEffect
{
}

class MulticastEffect : BaseEffect
{
}

class SelectEffect : BaseEffect
{
}

class User
{
    void Main()
    {
        var ledline1 = new LedLine();
        var ledline2 = new LedLine();

        var multicastEffect = new MulticastEffect();
        multicastEffect.OutputSlots["colorBuffer"][0] = ledline1.InputHandles["colorBuffer"];
        multicastEffect.OutputSlots["outputColor"][1] = ledline2.InputHandles["colorBuffer"];

        var overlayEffect = new OverlayEffect();
        overlayEffect.OutputSlots[0] = multicastEffect.InputHandles[0];

        var cubeEffect = new CubeEffect();
        cubeEffect.OutputSlots[0] = overlayEffect.InputHandles[0];

        var rainbowEffect = new RainbowEffect();
        rainbowEffect.OutputSlots[0] = overlayEffect.InputHandles[1];
    }
}
