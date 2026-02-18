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
    void Cleanup();
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

    public virtual void Initialize(IEffectContext context)
    {
    }
    
    public virtual void Cleanup()
    {
    }

    public void BindOutputs(string outputSlotName, string inputHandleName, BaseEffect targetEffect)
    {
        IUntypedOutputSlot outputSlot = _outputs.Container[outputSlotName];
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
}

public interface IUntypedOutputSlot
{
    string Id { get; }
    Type SetValueType { get; }
    Type ValueType { get; }

    void SetSetValue(Delegate setValue);
    
    void Invoke(object? value);
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

    public void Invoke(T value) => _setValueDelegate?.Invoke(value);

    public void Invoke(object? value)
    {
        if (value is T typedValue)
        {
            Invoke(typedValue);
        }
        else
        {
            throw new InvalidOperationException(
                $"Invalid value type for output slot {Id}. Expected {typeof(T).Name}.");
        }
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

public class EffectOutputsCollection : IEffectOutputsCollection
{
    public readonly Dictionary<string, IUntypedOutputSlot> Container = new();

    public IReadOnlyDictionary<string, IUntypedOutputSlot> Entries => Container;

    public OutputSlot<T> RegisterOutput<T>(string name)
    {
        OutputSlot<T> outputSlot = new(name);
        Container[name] = outputSlot;
        return outputSlot;
    }
    
    public IUntypedOutputSlot RegisterOutput(string name, Type valueType)
    {
        Type outputSlotType = typeof(OutputSlot<>).MakeGenericType(valueType);
        IUntypedOutputSlot outputSlot = (IUntypedOutputSlot)Activator.CreateInstance(outputSlotType, name)!;
        Container[name] = outputSlot;
        return outputSlot;
    }
}

public readonly record struct Index2D(int Row, int Col);

public interface IReadOnlyBuffer2D<T>
{
    T this[Index2D index] { get; }
    int Rows { get; }
    int Cols { get; }
}

public readonly struct Buffer2D<T> : IReadOnlyBuffer2D<T>
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
    private readonly List<TaskHandle> _tasks = [];
    
    public TaskHandle ScheduleTask(Action taskAction, int fps)
    {
        ArgumentNullException.ThrowIfNull(taskAction);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fps);

        TaskHandle handle = new();
        _tasks.Add(handle);

        handle.Thread = new Thread(() =>
        {
            FpsStableLoop(taskAction, fps, handle);
        }) { IsBackground = true };

        handle.Thread.Start();
        
        return handle;
    }
    
    public void StopAll()
    {
        foreach (TaskHandle handle in _tasks)
        {
            handle.Stop();
        }
        _tasks.Clear();
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
