using System.Reflection;
using XywireHost.Core.core;

namespace XywireHost.Core.Graph;

public sealed record EffectGraphNode(
    int Id,
    string TypeId,
    float X,
    float Y,
    Dictionary<string, object?>? EmbeddedInputValues = null);

public sealed record EffectGraphConnection(int FromNodeId, string FromPort, int ToNodeId, string ToPort);

public sealed class EffectGraphModel
{
    public List<EffectGraphNode> Nodes { get; } = [];
    public List<EffectGraphConnection> Connections { get; } = [];
}

public sealed class ConcreteEffectDescriptor(
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

public sealed class GenericEffectDescriptor(
    string baseTypeId,
    string displayName,
    Type genericTypeDefinition,
    int genericParameterCount)
{
    public string BaseTypeId { get; } = baseTypeId;
    public string DisplayName { get; } = displayName;
    public Type GenericTypeDefinition { get; } = genericTypeDefinition;
    public int GenericParameterCount { get; } = genericParameterCount;
    
    public ConcreteEffectDescriptor MakeConcreteDescriptor(params Type[] typeArguments)
    {
        if (typeArguments.Length != GenericParameterCount)
            throw new ArgumentException($"Expected {GenericParameterCount} type arguments, got {typeArguments.Length}");

        Type closedType = GenericTypeDefinition.MakeGenericType(typeArguments);
        string typeArgsString = string.Join(", ", typeArguments.Select(t => t.Name));
        string typeId = $"{BaseTypeId}<{typeArgsString}>";
        string name = $"{DisplayName}<{typeArgsString}>";

        return EffectNodeCatalog.EffectTypeToDescriptor(closedType, typeId, name);
    }
}

public sealed record EmbeddedInputDescriptor(string Name, Type ValueType);

public sealed record AvailableTypeDescriptor(string DisplayName, Type Type);

public static class EffectNodeCatalog
{
    public static IReadOnlyList<AvailableTypeDescriptor> AvailableGenericTypes { get; } =
    [
        new("int", typeof(int)),
        new("float", typeof(float)),
        new("double", typeof(double)),
        new("bool", typeof(bool)),
        new("string", typeof(string)),
        new("Color", typeof(Color)),
        new("LedLine", typeof(LedLine)),
    ];

    private static readonly Dictionary<string, ConcreteEffectDescriptor> ConcreteDescriptors = new();
    private static readonly List<GenericEffectDescriptor> GenericDescriptors = [];

    static EffectNodeCatalog()
    {
        LoadFromAssembly(Assembly.GetExecutingAssembly());
    }
    
    public static IReadOnlyList<ConcreteEffectDescriptor> All => ConcreteDescriptors.Values.ToList();
    public static IReadOnlyList<GenericEffectDescriptor> AllGeneric => GenericDescriptors;

    public static ConcreteEffectDescriptor? TryGet(string typeId) =>
        ConcreteDescriptors.GetValueOrDefault(typeId);
    
    public static void RegisterConcreteDescriptor(ConcreteEffectDescriptor descriptor)
    {
        ConcreteDescriptors[descriptor.TypeId] = descriptor;
    }

    private static void LoadFromAssembly(Assembly assembly)
    {
        foreach (Type type in assembly.GetTypes())
        {
            if (!typeof(BaseEffect).IsAssignableFrom(type) || type.IsAbstract) continue;

            if (type.IsGenericTypeDefinition)
            {
                // Generic type - add to generic descriptors
                GenericEffectDescriptor genericDescriptor = new(
                    type.Name.Split('`')[0], // Remove `1, `2 etc. suffix
                    type.Name.Split('`')[0],
                    type,
                    type.GetGenericArguments().Length
                );
                GenericDescriptors.Add(genericDescriptor);
            }
            else
            {
                // Concrete type - create descriptor and add to catalog
                ConcreteEffectDescriptor descriptor = EffectTypeToDescriptor(type, type.Name, type.Name);
                ConcreteDescriptors[descriptor.TypeId] = descriptor;
            }
        }
    }

    internal static ConcreteEffectDescriptor EffectTypeToDescriptor(Type type, string typeId, string displayName)
    {
        object? createdInstance = Activator.CreateInstance(type);
        if (createdInstance is BaseEffect effectInstance)
        {
            return new ConcreteEffectDescriptor(
                typeId,
                displayName,
                effectInstance.Inputs.Keys.ToList(),
                effectInstance.Outputs.Keys.ToList(),
                effectInstance.EmbeddedInputs
                    .Select(ei => new EmbeddedInputDescriptor(ei.Key, ei.Value.ValueType))
                    .ToList(),
                () => (IEffectNodeInstance)Activator.CreateInstance(type)!
            );
        }

        throw new InvalidOperationException($"Type {type.Name} is not a valid effect type.");
    }
}

public sealed class EffectGraphCompilationResult
{
    public EffectGraphCompilationResult(
        IReadOnlyDictionary<int, IEffectNodeInstance> instances,
        IReadOnlyList<string> errors)
    {
        Instances = instances;
        Errors = errors;
    }

    public IReadOnlyDictionary<int, IEffectNodeInstance> Instances { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool Success => Errors.Count == 0;
}

public static class EffectGraphCompiler
{
    public static EffectGraphCompilationResult Compile(EffectGraphModel graph, IEffectContext context)
    {
        List<string> errors = [];
        Dictionary<int, IEffectNodeInstance> instances = new();
        Dictionary<int, EffectGraphNode> nodeDataMap = new();

        foreach (EffectGraphNode node in graph.Nodes)
        {
            ConcreteEffectDescriptor? descriptor = EffectNodeCatalog.TryGet(node.TypeId);
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
        foreach ((int nodeId, IEffectNodeInstance instance) in instances)
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
