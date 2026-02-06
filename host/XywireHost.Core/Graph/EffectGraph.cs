using System.Collections.ObjectModel;
using XywireHost.Core;
using XywireHost.Core.core;

namespace XywireHost.Core.Graph;

public sealed record EffectGraphNode(Guid Id, string TypeId, float X, float Y, object? Data);

public sealed record EffectGraphConnection(Guid FromNodeId, string FromPort, Guid ToNodeId, string ToPort);

public sealed class EffectGraphModel
{
    public List<EffectGraphNode> Nodes { get; } = [];
    public List<EffectGraphConnection> Connections { get; } = [];
}

public sealed class EffectNodeDescriptor
{
    public EffectNodeDescriptor(
        string typeId,
        string displayName,
        IReadOnlyList<string> inputs,
        IReadOnlyList<string> outputs,
        Func<object?, IEffectNodeInstance> createInstance,
        Type? dataType = null)
    {
        TypeId = typeId;
        DisplayName = displayName;
        Inputs = inputs;
        Outputs = outputs;
        CreateInstance = createInstance;
        DataType = dataType;
    }

    public string TypeId { get; }
    public string DisplayName { get; }
    public IReadOnlyList<string> Inputs { get; }
    public IReadOnlyList<string> Outputs { get; }
    public Func<object?, IEffectNodeInstance> CreateInstance { get; }
    public Type? DataType { get; }
}

public static class EffectNodeCatalog
{
    private static readonly IReadOnlyList<EffectNodeDescriptor> Catalog = new List<EffectNodeDescriptor>
    {
        new(
            "Rainbow",
            "Rainbow",
            ["width", "height", "fps"],
            ["colorBuffer"],
            _ => new RainbowEffect()),
        new(
            "Cube",
            "Cube",
            Array.Empty<string>(),
            ["colorBuffer"],
            _ => new CubeEffect()),
        new(
            "Overlay",
            "Overlay",
            ["colorBuffer0", "colorBuffer1"],
            ["colorBuffer"],
            _ => new OverlayEffect()),
        new(
            "Multicast",
            "Multicast",
            ["colorBuffer"],
            ["colorBuffer0", "colorBuffer1"],
            _ => new MulticastEffect()),
        new(
            "ConstantInt",
            "Constant (int)",
            Array.Empty<string>(),
            ["value"],
            data => new ConstantEffect<int>(data is int value ? value : 0),
            typeof(int)),
        new(
            "LedLine",
            "Led Line",
            ["colorBuffer"],
            [],
            data => new LedLineNode(data is LedLine value ? value : null)),
    };

    private static readonly IReadOnlyDictionary<string, EffectNodeDescriptor> CatalogLookup =
        new ReadOnlyDictionary<string, EffectNodeDescriptor>(Catalog.ToDictionary(node => node.TypeId));

    public static IReadOnlyList<EffectNodeDescriptor> All => Catalog;

    public static EffectNodeDescriptor? TryGet(string typeId) =>
        CatalogLookup.TryGetValue(typeId, out EffectNodeDescriptor? descriptor) ? descriptor : null;
}

public sealed class EffectGraphCompilationResult
{
    public EffectGraphCompilationResult(
        IReadOnlyDictionary<Guid, IEffectNodeInstance> instances,
        IReadOnlyList<string> errors)
    {
        Instances = instances;
        Errors = errors;
    }

    public IReadOnlyDictionary<Guid, IEffectNodeInstance> Instances { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool Success => Errors.Count == 0;
}

public static class EffectGraphCompiler
{
    public static EffectGraphCompilationResult Compile(EffectGraphModel graph, IEffectContext context)
    {
        List<string> errors = new();
        Dictionary<Guid, IEffectNodeInstance> instances = new();

        foreach (EffectGraphNode node in graph.Nodes)
        {
            EffectNodeDescriptor? descriptor = EffectNodeCatalog.TryGet(node.TypeId);
            if (descriptor == null)
            {
                errors.Add($"Unknown node type '{node.TypeId}' for node {node.Id}.");
                continue;
            }

            if (descriptor.DataType != null && node.Data != null &&
                !descriptor.DataType.IsInstanceOfType(node.Data))
            {
                errors.Add(
                    $"Node {node.Id} expects data type {descriptor.DataType.Name} but got {node.Data.GetType().Name}.");
                continue;
            }

            IEffectNodeInstance instance = descriptor.CreateInstance(node.Data);
            instances[node.Id] = instance;
        }

        foreach (EffectGraphConnection connection in graph.Connections)
        {
            if (!instances.TryGetValue(connection.FromNodeId, out IEffectNodeInstance? fromNode) ||
                !instances.TryGetValue(connection.ToNodeId, out IEffectNodeInstance? toNode))
            {
                errors.Add("Connection references missing nodes.");
                continue;
            }

            if (!fromNode.Outputs.TryGetValue(connection.FromPort, out IUntypedOutputSlot? outputSlot))
            {
                errors.Add($"Output '{connection.FromPort}' not found on node {connection.FromNodeId}.");
                continue;
            }

            if (!toNode.Inputs.TryGetValue(connection.ToPort, out IUntypedInputHandle? inputHandle))
            {
                errors.Add($"Input '{connection.ToPort}' not found on node {connection.ToNodeId}.");
                continue;
            }

            if (outputSlot.ValueType != inputHandle.ValueType)
            {
                errors.Add(
                    $"Type mismatch {outputSlot.ValueType.Name} -> {inputHandle.ValueType.Name} for connection {connection.FromNodeId} -> {connection.ToNodeId}.");
                continue;
            }

            if (inputHandle.IsConnected)
            {
                errors.Add($"Input '{connection.ToPort}' on node {connection.ToNodeId} is already connected.");
                continue;
            }

            outputSlot.SetSetValue(inputHandle.GetSetValue());
            inputHandle.IsConnected = true;
        }

        foreach (IEffectNodeInstance instance in instances.Values)
        {
            instance.Initialize(context);
        }

        return new EffectGraphCompilationResult(instances, errors);
    }
}
