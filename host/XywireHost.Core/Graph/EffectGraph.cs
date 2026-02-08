using System.Collections.ObjectModel;
using System.Reflection;
using XywireHost.Core.core;

namespace XywireHost.Core.Graph;

public sealed record EffectGraphNode(
    Guid Id,
    string TypeId,
    float X,
    float Y,
    Dictionary<string, object?>? EmbeddedInputValues = null);

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
    IReadOnlyList<EmbeddedInputDescriptor> embeddedInputs,
    Func<IEffectNodeInstance> createInstance)
{
    public string TypeId { get; } = typeId;
    public string DisplayName { get; } = displayName;
    public IReadOnlyList<string> Inputs { get; } = inputs;
    public IReadOnlyList<string> Outputs { get; } = outputs;
    public IReadOnlyList<EmbeddedInputDescriptor> EmbeddedInputs { get; } = embeddedInputs;
    public Func<IEffectNodeInstance> CreateInstance { get; } = createInstance;
}

public sealed record EmbeddedInputDescriptor(string Name, Type ValueType);

public static class EffectNodeCatalog
{
    public static IReadOnlyList<EffectNodeDescriptor> All { get; } = 
        GetDescriptorsFromAssembly(Assembly.GetExecutingAssembly());
    
    private static readonly IReadOnlyDictionary<string, EffectNodeDescriptor> CatalogLookup =
        new ReadOnlyDictionary<string, EffectNodeDescriptor>(All.ToDictionary(node => node.TypeId));

    public static EffectNodeDescriptor? TryGet(string typeId) =>
        CatalogLookup.GetValueOrDefault(typeId);

    private static List<EffectNodeDescriptor> GetDescriptorsFromAssembly(Assembly assembly)
    {
        List<EffectNodeDescriptor> descriptors = [];

        foreach (Type type in assembly.GetTypes())
        {
            if (!typeof(BaseEffect).IsAssignableFrom(type) || type.IsAbstract) continue;
            
            EffectNodeDescriptor descriptor = EffectTypeToDescriptor(type);
            descriptors.Add(descriptor);
        }

        return descriptors;
    }
    
    private static EffectNodeDescriptor EffectTypeToDescriptor(Type type)
    {
        object? createdInstance = Activator.CreateInstance(type, [type]);
        if (createdInstance is BaseEffect effectInstance)
        {
            return new EffectNodeDescriptor(
                type.Name,
                type.Name,
                effectInstance.Inputs.Keys.ToList(),
                effectInstance.Outputs.Keys.ToList(),
                effectInstance.EmbeddedInputs.Select(ei => new EmbeddedInputDescriptor(ei.Key, ei.Value.ValueType)).ToList(),
                () => (IEffectNodeInstance)Activator.CreateInstance(type)!
            );
        }
        
        throw new InvalidOperationException($"Type {type.Name} is not a valid effect type.");
    }
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
        Dictionary<Guid, EffectGraphNode> nodeDataMap = new();

        foreach (EffectGraphNode node in graph.Nodes)
        {
            EffectNodeDescriptor? descriptor = EffectNodeCatalog.TryGet(node.TypeId);
            if (descriptor == null)
            {
                errors.Add($"Unknown node type '{node.TypeId}' for node {node.Id}.");
                continue;
            }

            // Validate embedded input values types
            if (node.EmbeddedInputValues != null)
            {
                foreach ((string inputName, object? value) in node.EmbeddedInputValues)
                {
                    EmbeddedInputDescriptor? embeddedInput = descriptor.EmbeddedInputs
                        .FirstOrDefault(e => e.Name == inputName);

                    if (embeddedInput == null)
                    {
                        errors.Add($"Unknown embedded input '{inputName}' for node {node.Id}.");
                        continue;
                    }

                    if (value != null && !embeddedInput.ValueType.IsInstanceOfType(value))
                    {
                        errors.Add(
                            $"Embedded input '{inputName}' on node {node.Id} expects type {embeddedInput.ValueType.Name} but got {value.GetType().Name}.");
                    }
                }
            }

            IEffectNodeInstance instance = descriptor.CreateInstance();
            instances[node.Id] = instance;
            nodeDataMap[node.Id] = node;
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

        // Invoke embedded inputs with provided values
        foreach ((Guid nodeId, IEffectNodeInstance instance) in instances)
        {
            if (!nodeDataMap.TryGetValue(nodeId, out EffectGraphNode? node))
                continue;

            if (node.EmbeddedInputValues == null)
                continue;

            foreach ((string inputName, object? value) in node.EmbeddedInputValues)
            {
                if (value == null)
                    continue;

                if (!instance.EmbeddedInputs.TryGetValue(inputName, out IUntypedInputHandle? inputHandle))
                    continue;

                // Get the SetValue delegate and invoke it with the value
                Delegate setValue = inputHandle.GetSetValue();
                setValue.DynamicInvoke(value);
            }
        }

        foreach (IEffectNodeInstance instance in instances.Values)
        {
            instance.Initialize(context);
        }

        return new EffectGraphCompilationResult(instances, errors);
    }
}
