using System.Collections.ObjectModel;
using System.Reflection;
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

public sealed class EffectNodeDescriptor(
    string typeId,
    string displayName,
    IReadOnlyList<string> inputs,
    IReadOnlyList<string> outputs,
    Func<IEffectNodeInstance> createInstance,
    Type? dataType = null)
{
    public string TypeId { get; } = typeId;
    public string DisplayName { get; } = displayName;
    public IReadOnlyList<string> Inputs { get; } = inputs;
    public IReadOnlyList<string> Outputs { get; } = outputs;
    public Func<IEffectNodeInstance> CreateInstance { get; } = createInstance;
    public Type? DataType { get; } = dataType;
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
            () => new RainbowEffect()),
        new(
            "Cube",
            "Cube",
            [],
            ["colorBuffer"],
            () => new CubeEffect()),
        new(
            "Overlay",
            "Overlay",
            ["colorBuffer0", "colorBuffer1"],
            ["colorBuffer"],
            () => new OverlayEffect()),
        new(
            "Multicast",
            "Multicast",
            ["colorBuffer"],
            ["colorBuffer0", "colorBuffer1"],
            () => new MulticastEffect()),
        new(
            "ConstantInt",
            "Constant (int)",
            [],
            ["value"],
            () => new ConstantEffect<int>(),
            typeof(int)),
        new(
            "LedLine",
            "Led Line",
            ["colorBuffer"],
            [],
            () => new LedLineNode(new("52"))),
    };

    private static readonly IReadOnlyDictionary<string, EffectNodeDescriptor> CatalogLookup =
        new ReadOnlyDictionary<string, EffectNodeDescriptor>(Catalog.ToDictionary(node => node.TypeId));

    public static IReadOnlyList<EffectNodeDescriptor> All => Catalog;

    public static EffectNodeDescriptor? TryGet(string typeId) =>
        CatalogLookup.GetValueOrDefault(typeId);
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
        List<string> errors = [];
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

            IEffectNodeInstance instance = descriptor.CreateInstance();
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
