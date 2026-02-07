using System.Diagnostics;
using XywireHost.Core.core;

namespace XywireHost.Core;

public interface IDataSink
{
    IEffectInputsCollection InputHandles { get; }
}

public interface IDataSource
{
    IEffectOutputsCollection OutputSlots { get; }
}

public interface IEffectContext
{
    Scheduler Scheduler { get; }
}

public interface IEffectNodeInstance
{
    IReadOnlyDictionary<string, IUntypedInputHandle> EmbeddedInputs { get; }
    IReadOnlyDictionary<string, IUntypedInputHandle> Inputs { get; }
    IReadOnlyDictionary<string, IUntypedOutputSlot> Outputs { get; }
    void Initialize(IEffectContext context);
}

public class EffectContext : IEffectContext
{
    public Scheduler Scheduler { get; } = new();
}

public abstract class BaseEffect : IDataSink, IDataSource, IEffectNodeInstance
{
    private readonly EffectInputsCollection _embeddedInputs = new();
    private readonly EffectInputsCollection _inputs = new();
    private readonly EffectOutputsCollection _outputs = new();

    public IEffectInputsCollection EmbeddedInputHandles => _embeddedInputs;
    public IEffectInputsCollection InputHandles => _inputs;
    public IEffectOutputsCollection OutputSlots => _outputs;

    public IReadOnlyDictionary<string, IUntypedInputHandle> EmbeddedInputs => _embeddedInputs.Entries;
    public IReadOnlyDictionary<string, IUntypedInputHandle> Inputs => _inputs.Entries;
    public IReadOnlyDictionary<string, IUntypedOutputSlot> Outputs => _outputs.Entries;

    public void BindOutputs(string outputSlotName, string inputHandleName, BaseEffect targetEffect)
    {
        IUntypedOutputSlot outputSlot = _outputs._container[outputSlotName];
        IUntypedInputHandle inputHandle = targetEffect._inputs._container[inputHandleName];
        BindOutputs(outputSlot, inputHandle);
    }

    public static void BindOutputs(IUntypedOutputSlot outputSlot, IUntypedInputHandle inputHandle)
    {
        if (inputHandle.IsConnected)
            throw new InvalidOperationException($"Input handle {inputHandle.Id} is already connected.");

        outputSlot.SetSetValue(inputHandle.GetSetValue());
        inputHandle.IsConnected = true;
    }

    // TODO errors list reporting
    public bool AllInputsConnected() => _inputs._container.Values.All(inputHandle => inputHandle.IsConnected);

    public bool NoInputs() => _inputs._container.Count == 0;

    public virtual void Initialize(IEffectContext context)
    {
    }
}

public interface IUntypedOutputSlot
{
    string Id { get; }
    Type SetValueType { get; }
    Type ValueType { get; }

    void SetSetValue(Delegate setValue);
}

public interface IUntypedInputHandle
{
    string Id { get; }
    Type SetValueType { get; }
    Type ValueType { get; }
    bool IsConnected { get; set; }
    Delegate GetSetValue();
}

public class OutputSlot<T>(string id) : IUntypedOutputSlot
{
    private Action<T>? _setValueDelegate;
    public string Id { get; } = id;
    public Type SetValueType { get; } = typeof(Action<T>);
    public Type ValueType { get; } = typeof(T);

    public void SetSetValue(Delegate setValue)
    {
        if (setValue is Action<T> typedSetValue)
        {
            _setValueDelegate = typedSetValue;
        }
        else
        {
            throw new InvalidOperationException(
                $"Invalid set value type for output slot {Id}. Expected Action<{typeof(T).Name}>.");
        }
    }

    public void Invoke(T value)
    {
        _setValueDelegate?.Invoke(value);
    }
}

internal class InputHandle<T>(string id, Action<T> valueChangeHandler) : IUntypedInputHandle
{
    private Action<T> ValueChangeHandler { get; } = valueChangeHandler;
    public string Id { get; } = id;
    public Type SetValueType { get; } = typeof(Action<T>);
    public Type ValueType { get; } = typeof(T);
    public bool IsConnected { get; set; }


    public Delegate GetSetValue() => ValueChangeHandler;
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

    public IReadOnlyDictionary<string, IUntypedInputHandle> Entries => _container;

    public void RegisterInput<T>(string name, Action<T> callback) =>
        _container[name] = new InputHandle<T>(name, callback);
}

internal class EffectOutputsCollection : IEffectOutputsCollection
{
    internal Dictionary<string, IUntypedOutputSlot> _container = new();

    public IReadOnlyDictionary<string, IUntypedOutputSlot> Entries => _container;

    public OutputSlot<T> RegisterOutput<T>(string name)
    {
        OutputSlot<T> outputSlot = new(name);
        _container[name] = outputSlot;
        return outputSlot;
    }
}

internal readonly record struct Index2D(int Row, int Col);

internal interface IReadOnlyBuffer2D<T>
{
    T this[Index2D index] { get; }
    int Rows { get; }
    int Cols { get; }
}

internal readonly struct Buffer2D<T> : IReadOnlyBuffer2D<T>
{
    private readonly T[,] _data;

    public int Rows => _data.GetLength(0);
    public int Cols => _data.GetLength(1);

    public Buffer2D(int rows, int cols)
    {
        _data = new T[rows, cols];
    }

    public T this[Index2D index]
    {
        get => _data[index.Row, index.Col];
        set => _data[index.Row, index.Col] = value;
    }

    public void ForeachIndexed(Action<Index2D, T> action)
    {
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                action(new Index2D(row, col), _data[row, col]);
            }
        }
    }
    
    public void ShiftDown()
    {
        for (int row = Rows - 1; row >= 1; row--)
        {
            for (int col = 0; col < Cols; col++)
            {
                _data[row, col] = _data[row - 1, col];
            }
        }
    }
}

public class TaskHandle
{
    internal volatile bool Running = true;
    internal Thread? Thread;

    public void Stop()
    {
        Running = false;
        Thread?.Join();
    }
}

public class Scheduler
{
    public TaskHandle ScheduleTask(Action taskAction, int fps)
    {
        ArgumentNullException.ThrowIfNull(taskAction);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fps);

        TaskHandle handle = new();

        handle.Thread = new Thread(() =>
        {
            FpsStableLoop(taskAction, fps, handle);
        }) { IsBackground = true };

        handle.Thread.Start();
        return handle;
    }

    private void FpsStableLoop(Action taskAction, int fps, TaskHandle handle)
    {
        double frameTimeMs = 1000.0 / fps;
        Stopwatch sw = Stopwatch.StartNew();

        double nextFrameTime = sw.Elapsed.TotalMilliseconds;

        while (handle.Running)
        {
            taskAction();

            nextFrameTime += frameTimeMs;

            while (handle.Running && sw.Elapsed.TotalMilliseconds < nextFrameTime)
            {
                double remaining = nextFrameTime - sw.Elapsed.TotalMilliseconds;

                if (remaining > 2.0)
                    Thread.Sleep(1);
                else
                    Thread.SpinWait(10);
            }
            
            if (sw.Elapsed.TotalMilliseconds > nextFrameTime + frameTimeMs)
            {
                nextFrameTime = sw.Elapsed.TotalMilliseconds;
            }
        }
    }
}

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
            _colorBuffer[new Index2D(0, i)] = Color.HSV((_counter * stepHueOffset) % 360, 1, 1);
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

internal class CubeEffect : BaseEffect
{
    private readonly OutputSlot<IReadOnlyBuffer2D<Color>> _colorsOutput;

    public CubeEffect()
    {
        _colorsOutput = OutputSlots.RegisterOutput<IReadOnlyBuffer2D<Color>>("colorBuffer");
    }
}

internal class OverlayEffect : BaseEffect
{
    private readonly OutputSlot<IReadOnlyBuffer2D<Color>> _colorsOutput;

    public OverlayEffect()
    {
        _colorsOutput = OutputSlots.RegisterOutput<IReadOnlyBuffer2D<Color>>("colorBuffer");

        InputHandles.RegisterInput<IReadOnlyBuffer2D<Color>>("colorBuffer0", SetColorBuffer0);
        InputHandles.RegisterInput<IReadOnlyBuffer2D<Color>>("colorBuffer1", SetColorBuffer1);
    }

    private void SetColorBuffer0(IReadOnlyBuffer2D<Color> buffer) => _colorsOutput.Invoke(buffer);

    private void SetColorBuffer1(IReadOnlyBuffer2D<Color> buffer) => _colorsOutput.Invoke(buffer);
}

internal class MulticastEffect : BaseEffect
{
    private readonly OutputSlot<IReadOnlyBuffer2D<Color>> _colorsOutput0;
    private readonly OutputSlot<IReadOnlyBuffer2D<Color>> _colorsOutput1;

    public MulticastEffect()
    {
        _colorsOutput0 = OutputSlots.RegisterOutput<IReadOnlyBuffer2D<Color>>("colorBuffer0");
        _colorsOutput1 = OutputSlots.RegisterOutput<IReadOnlyBuffer2D<Color>>("colorBuffer1");

        InputHandles.RegisterInput<IReadOnlyBuffer2D<Color>>("colorBuffer", SetColorBuffer);
    }

    private void SetColorBuffer(IReadOnlyBuffer2D<Color> buffer)
    {
        _colorsOutput0.Invoke(buffer);
        _colorsOutput1.Invoke(buffer);
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

internal class User
{
    private static readonly List<BaseEffect> Effects = [];

    private static T CreateEffect<T>(Func<T> creator) where T : BaseEffect
    {
        T result = creator();
        Effects.Add(result);
        return result;
    }

    public static void Main()
    {
        LedLineEffect ledline1 = new();
        LedLineEffect ledline2 = new();

        MulticastEffect multicastEffect = CreateEffect(() => new MulticastEffect());
        // TODO recursive output collections or at least arrays
        multicastEffect.BindOutputs("colorBuffer0", "colorBuffer", ledline1);
        multicastEffect.BindOutputs("colorBuffer1", "colorBuffer", ledline2);

        OverlayEffect overlayEffect = CreateEffect(() => new OverlayEffect());
        overlayEffect.BindOutputs("colorBuffer", "colorBuffer", multicastEffect);

        CubeEffect cubeEffect = CreateEffect(() => new CubeEffect());
        cubeEffect.BindOutputs("colorBuffer", "colorBuffer0", overlayEffect);

        RainbowEffect rainbowEffect = CreateEffect(() => new RainbowEffect());
        rainbowEffect.BindOutputs("colorBuffer", "colorBuffer1", overlayEffect);

        ConstantEffect<int> widthEffect = CreateEffect(() => new ConstantEffect<int>());
        widthEffect.BindOutputs("value", "width", rainbowEffect);
        ConstantEffect<int> heightEffect = CreateEffect(() => new ConstantEffect<int>());
        heightEffect.BindOutputs("value", "height", rainbowEffect);
        ConstantEffect<int> fpsEffect = CreateEffect(() => new ConstantEffect<int>());
        fpsEffect.BindOutputs("value", "fps", rainbowEffect);
        
        EffectOutputsCollection systemOutputs = new();
        
        OutputSlot<int> widthOutput = systemOutputs.RegisterOutput<int>("value");
        BaseEffect.BindOutputs(widthOutput, widthEffect.EmbeddedInputs["value"]);
        
        OutputSlot<int> heightOutput = systemOutputs.RegisterOutput<int>("value");
        BaseEffect.BindOutputs(heightOutput, heightEffect.EmbeddedInputs["value"]);
        
        OutputSlot<int> fpsOutput = systemOutputs.RegisterOutput<int>("value");
        BaseEffect.BindOutputs(fpsOutput, fpsEffect.EmbeddedInputs["value"]);
        
        OutputSlot<LedLine> ledLineOutput1 = systemOutputs.RegisterOutput<LedLine>("value");
        BaseEffect.BindOutputs(ledLineOutput1, ledline1.EmbeddedInputs["ledLine"]);
        
        OutputSlot<LedLine> ledLineOutput2 = systemOutputs.RegisterOutput<LedLine>("value");
        BaseEffect.BindOutputs(ledLineOutput2, ledline2.EmbeddedInputs["ledLine"]);

        foreach (BaseEffect effect in Effects)
        {
            if (!effect.AllInputsConnected())
            {
                throw new InvalidOperationException("Not all inputs are connected for effect " + effect.GetType().Name);
            }
        }
        
        widthOutput.Invoke(14);
        heightOutput.Invoke(14);
        fpsOutput.Invoke(5);
        ledLineOutput1.Invoke(new LedLine("192.168.1.65"));
        ledLineOutput2.Invoke(new LedLine("127.0.0.1"));
        
        IEffectContext context = new EffectContext();
        foreach (BaseEffect effect in Effects)
        {
            effect.Initialize(context);
        }

        Console.ReadKey();
    }
}
