using System.Diagnostics;
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

public interface IEffectContext
{
    Scheduler Scheduler { get; }
}

internal class EffectContext : IEffectContext
{
    public Scheduler Scheduler { get; } = new();
}

internal abstract class BaseEffect : IDataSource, IDataSink
{
    private readonly EffectOutputsCollection _outputs = new();
    private readonly EffectInputsCollection _inputs = new();
    public IEffectInputsCollection InputHandles => _inputs;

    public IEffectOutputsCollection OutputSlots => _outputs;

    public void BindOutputs(string outputSlotName, string inputHandleName, BaseEffect targetEffect)
    {
        IUntypedOutputSlot outputSlot = _outputs._container[outputSlotName];
        IUntypedInputHandle inputHandle = targetEffect._inputs._container[inputHandleName];
        BindOutputs(outputSlot, inputHandle);
    }

    public void BindOutputs(string outputSlotName, string inputHandleName, LedLine targetEffect)
    {
        IUntypedOutputSlot outputSlot = _outputs._container[outputSlotName];
        IUntypedInputHandle inputHandle = targetEffect.Inputs._container[inputHandleName];
        BindOutputs(outputSlot, inputHandle);
    }

    private static void BindOutputs(IUntypedOutputSlot outputSlot, IUntypedInputHandle inputHandle)
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

internal class LedLine : IDataSink
{
    internal readonly EffectInputsCollection Inputs = new();

    public LedLine()
    {
        Inputs.RegisterInput<IReadOnlyBuffer2D<Color>>("colorBuffer", SetColorBuffer);
    }

    public IEffectInputsCollection InputHandles => Inputs;

    // TODO actually it should accept 1D buffer
    private void SetColorBuffer(IReadOnlyBuffer2D<Color> buffer)
    {
        for (int row = 0; row < buffer.Rows; row++)
        {
            for (int col = 0; col < buffer.Cols; col++)
            {
                Color color = buffer[new Index2D(row, col)];
                Console.Write(color);
            }

            Console.WriteLine();
        }
    }
}

internal interface IUntypedOutputSlot
{
    string Id { get; }
    Type SetValueType { get; }
    Type ValueType { get; }

    void SetSetValue(Delegate setValue);
}

internal interface IUntypedInputHandle
{
    string Id { get; }
    Type SetValueType { get; }
    Type ValueType { get; }
    internal bool IsConnected { get; set; }
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
        if (_setValueDelegate == null)
            throw new InvalidOperationException($"Output slot {Id} is not connected to any input.");
        _setValueDelegate!.Invoke(value);
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

    public void RegisterInput<T>(string name, Action<T> callback) =>
        _container[name] = new InputHandle<T>(name, callback);
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
    private readonly T _value;

    public ConstantEffect(T value)
    {
        _value = value;
        _outputSlot = OutputSlots.RegisterOutput<T>("value");
    }

    public override void Initialize(IEffectContext context) => _outputSlot.Invoke(_value);
}

internal class RainbowEffect : BaseEffect
{
    private readonly OutputSlot<IReadOnlyBuffer2D<Color>> _colorsOutput;
    private Buffer2D<Color> _colorBuffer;
    
    private IEffectContext _context;
    private TaskHandle? _renderTaskHandle;

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

    public override void Initialize(IEffectContext context)
    {
        _context = context;
        Restart();
    }

    private void Render() => _colorsOutput.Invoke(_colorBuffer);

    private void Restart()
    {
        _renderTaskHandle?.Stop();
        
        if (_width <= 0 || _height <= 0 || _fps <= 0) return;
        
        _colorBuffer = new Buffer2D<Color>(_height, _width);
        _renderTaskHandle = _context.Scheduler.ScheduleTask(Render, _fps);
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

internal class User
{
    private static readonly List<BaseEffect> _effects = [];

    private static T CreateEffect<T>(Func<T> creator) where T : BaseEffect
    {
        T result = creator();
        _effects.Add(result);
        return result;
    }

    public static void Main()
    {
        LedLine ledline1 = new();
        LedLine ledline2 = new();

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

        ConstantEffect<int> widthEffect = CreateEffect(() => new ConstantEffect<int>(10));
        widthEffect.BindOutputs("value", "width", rainbowEffect);
        ConstantEffect<int> heightEffect = CreateEffect(() => new ConstantEffect<int>(10));
        heightEffect.BindOutputs("value", "height", rainbowEffect);
        ConstantEffect<int> fpsEffect = CreateEffect(() => new ConstantEffect<int>(30));
        fpsEffect.BindOutputs("value", "fps", rainbowEffect);

        foreach (BaseEffect effect in _effects)
        {
            if (!effect.AllInputsConnected())
            {
                throw new InvalidOperationException("Not all inputs are connected for effect " + effect.GetType().Name);
            }
        }
        
        IEffectContext context = new EffectContext();

        foreach (BaseEffect effect in _effects)
        {
            effect.Initialize(context);
        }

        Console.ReadKey();
    }
}
