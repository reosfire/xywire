using System.Collections.Immutable;
using System.Reflection;
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

public interface IDataSink
{
    IEffectInputsCollection InputHandles { get; }
}

public interface IDataSource
{
    IEffectOutputsCollection OutputSlots { get; }
}

class BaseEffect : IDataSource, IDataSink
{
    private readonly EffectOutputsCollection _outputs = new();
    private readonly EffectInputsCollection _inputs = new();

    public IEffectOutputsCollection OutputSlots => _outputs;
    public IEffectInputsCollection InputHandles => _inputs;
    
    public void BindOutputs(string outputSlotName, string inputHandleName, BaseEffect targetEffect)
    {
        IUntypedOutputSlot outputSlot = _outputs._container[outputSlotName];
        IUntypedInputHandle inputHandle = targetEffect._inputs._container[inputHandleName];
        
        outputSlot.SetSetValue(inputHandle.GetSetValue());
    }
    
    public void BindOutputs(string outputSlotName, string inputHandleName, LedLine targetEffect)
    {
        IUntypedOutputSlot outputSlot = _outputs._container[outputSlotName];
        IUntypedInputHandle inputHandle = targetEffect.Inputs._container[inputHandleName];
        
        outputSlot.SetSetValue(inputHandle.GetSetValue());
    }
}

class LedLine : IDataSink
{
    internal readonly EffectInputsCollection Inputs = new();

    public IEffectInputsCollection InputHandles => Inputs;
}

interface IUntypedOutputSlot
{
    string Id { get; }
    Type SetValueType { get; }
    Type ValueType { get; }

    void SetSetValue(Delegate setValue);
}

interface IUntypedInputHandle
{
    string Id { get; }
    Type SetValueType { get; }
    Type ValueType { get; }
    Delegate GetSetValue();
}

public class OutputSlot<T>(string id) : IUntypedOutputSlot
{
    public string Id { get; } = id;
    public Type SetValueType { get; } = typeof(Action<T>);
    public Type ValueType { get; } = typeof(T);

    private Action<T>? _setValueDelegate;

    public void Invoke(T value)
    {
        if (_setValueDelegate == null)
            throw new InvalidOperationException($"Output slot {Id} is not connected to any input.");
        _setValueDelegate!.Invoke(value);
    }

    public void SetSetValue(Delegate setValue)
    {
        if (setValue is Action<T> typedSetValue)
        {
            _setValueDelegate = typedSetValue;
        }
        else
        {
            throw new InvalidOperationException($"Invalid set value type for output slot {Id}. Expected Action<{typeof(T).Name}>.");
        }
    }
}

class InputHandle<T>(string id, Action<T> valueChangeHandler) : IUntypedInputHandle
{
    public string Id { get; } = id;
    public Type SetValueType { get; } = typeof(Action<T>);
    public Type ValueType { get; } = typeof(T);
    
    private Action<T> ValueChangeHandler { get; } = valueChangeHandler;

    public Delegate GetSetValue()
    {
        return ValueChangeHandler;
    }
}

public interface IEffectInputsCollection
{
    void RegisterInput<T>(string name, Action<T> callback);
}

public interface IEffectOutputsCollection
{
    OutputSlot<T> RegisterOutput<T>(string name);
}

internal class EffectInputsCollection : IEffectInputsCollection
{
    internal Dictionary<string, IUntypedInputHandle> _container = new();

    public void RegisterInput<T>(string name, Action<T> callback)
    {
        _container[name] = new InputHandle<T>(name, callback);
    }
}

internal class EffectOutputsCollection : IEffectOutputsCollection
{
    internal Dictionary<string, IUntypedOutputSlot> _container = new();

    public OutputSlot<T> RegisterOutput<T>(string name)
    {
        OutputSlot<T> outputSlot = new(name);
        _container[name] = outputSlot;
        return outputSlot;
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
        _colorsOutput.Invoke(_colorBuffer);
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
    public static void Main()
    {
        LedLine ledline1 = new();
        LedLine ledline2 = new();

        MulticastEffect multicastEffect = new();
        // TODO recursive output collections or at least arrays
        multicastEffect.BindOutputs("colorBuffer0", "colorBuffer", ledline1);
        multicastEffect.BindOutputs("colorBuffer1", "colorBuffer", ledline2);

        OverlayEffect overlayEffect = new();
        overlayEffect.BindOutputs("colorBuffer", "colorBuffer", multicastEffect);

        CubeEffect cubeEffect = new();
        cubeEffect.BindOutputs("colorBuffer", "colorBuffer", overlayEffect);

        RainbowEffect rainbowEffect = new();
        rainbowEffect.BindOutputs("colorBuffer", "colorBuffer", overlayEffect);
    }
}
