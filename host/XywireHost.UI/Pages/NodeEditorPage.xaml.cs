using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using SkiaSharp;
using XywireHost.Core;
using XywireHost.Core.Graph;
using XywireHost.Core.services;
using XywireHost.UI.Controls;
using Switch = Microsoft.Maui.Controls.Switch;

namespace XywireHost.UI.Pages;

public abstract record NodePickerItem(string DisplayName);

public sealed record ConcreteNodePickerItem(string DisplayName, ConcreteEffectDescriptor ConcreteDescriptor)
    : NodePickerItem(DisplayName);

public sealed record GenericNodePickerItem(string DisplayName, GenericEffectDescriptor GenericDescriptor)
    : NodePickerItem(DisplayName);

public record Payload(
    string TypeId,
    Dictionary<string, EmbeddedInputInfo> EmbeddedInputs
);

public class EmbeddedInputInfo(
    string name,
    Type valueType,
    IUntypedOutputSlot outputSlot,
    object? currentValue
)
{
    public string Name { get; } = name;
    public Type ValueType { get; } = valueType;
    public IUntypedOutputSlot OutputSlot { get; } = outputSlot;
    public object? CurrentValue { get; private set; } = currentValue;

    public void InvokeAndSetCurrentValue(object value)
    {
        OutputSlot.Invoke(value);
        CurrentValue = value;
    }
}

// Serializable graph models
public record SerializableEmbeddedInput(
    string Name,
    string ValueTypeName,
    object? Value
);

public record SerializableNode(
    int NodeId,
    string TypeId,
    float PositionX,
    float PositionY,
    List<SerializableEmbeddedInput> EmbeddedInputs
);

public record SerializableConnection(
    int FromNodeId,
    string FromPort,
    int ToNodeId,
    string ToPort
);

public record SerializableGraph(
    List<SerializableNode> Nodes,
    List<SerializableConnection> Connections
);

public partial class NodeEditorPage : ContentPage
{
    private readonly EffectService _effectService;

    private readonly List<NodePickerItem> _pickerItems = [];

    private readonly EffectOutputsCollection _systemOutputs = new();

    private int _outputId = 0;

    public NodeEditorPage(EffectService effectService)
    {
        InitializeComponent();

        _effectService = effectService;

        foreach (ConcreteEffectDescriptor descriptor in EffectNodeCatalog.All)
        {
            _pickerItems.Add(new ConcreteNodePickerItem(descriptor.DisplayName, descriptor));
        }

        foreach (GenericEffectDescriptor genericDescriptor in EffectNodeCatalog.AllGeneric)
        {
            _pickerItems.Add(new GenericNodePickerItem($"{genericDescriptor.DisplayName}<T>", genericDescriptor));
        }

        GenericTypePicker.ItemsSource = EffectNodeCatalog.AvailableGenericTypes
            .Select(t => t.DisplayName)
            .ToList();
        if (GenericTypePicker.ItemsSource.Count > 0)
        {
            GenericTypePicker.SelectedIndex = 0;
        }

        NodePicker.ItemsSource = _pickerItems.Select(item => item.DisplayName).ToList();
        if (NodePicker.ItemsSource.Count > 0)
        {
            NodePicker.SelectedIndex = 0;
        }

        SeedSampleGraph();
    }

    private void OnNodePickerSelectionChanged(object? sender, EventArgs e)
    {
        NodePickerItem? selectedItem = GetSelectedNodePickerItem();
        GenericTypePicker.IsVisible = selectedItem is GenericNodePickerItem;
    }

    private NodePickerItem? GetSelectedNodePickerItem()
    {
        if (NodePicker.SelectedIndex < 0 || NodePicker.SelectedIndex >= _pickerItems.Count)
            return null;
        return _pickerItems[NodePicker.SelectedIndex];
    }

    private NodeInstance<Payload> CreateNodeFromDescriptor(ConcreteEffectDescriptor prototype, SKPoint position)
    {
        Dictionary<string, EmbeddedInputInfo> embeddedInputs = prototype.EmbeddedInputs.ToDictionary(
            descriptor => descriptor.Name,
            descriptor => new EmbeddedInputInfo(
                descriptor.Name,
                descriptor.ValueType,
                _systemOutputs.RegisterOutput((_outputId++).ToString(), descriptor.ValueType),
                null
            )
        );

        NodeInstance<Payload> result = NodesView.AddNode(
            position,
            prototype.DisplayName,
            prototype.Inputs.ToList(),
            prototype.Outputs.ToList(),
            new Payload(
                prototype.TypeId,
                embeddedInputs
            )
        );

        return result;
    }

    private void SeedSampleGraph()
    {
        string graphPath = GetDefaultGraphPath();
        if (File.Exists(graphPath))
        {
            try
            {
                LoadGraphFromFile(graphPath);
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load graph: {ex.Message}");
            }
        }

        CreateDefaultSampleGraph();
        SaveGraphToFile(graphPath);
    }

    private void CreateDefaultSampleGraph()
    {
        EnsureGenericEffectRegistered("ConstantEffect", typeof(int));
        EnsureGenericEffectRegistered("ConstantEffect", typeof(double));
        EnsureGenericEffectRegistered("MulticastEffect", typeof(int));

        string intTypeName = typeof(int).Name; // "Int32"
        string doubleTypeName = typeof(double).Name; // "Double"

        if (!TryGetDescriptor($"ConstantEffect<{intTypeName}>", out ConcreteEffectDescriptor? constantInt) ||
            !TryGetDescriptor($"ConstantEffect<{doubleTypeName}>", out ConcreteEffectDescriptor? constantDouble) ||
            !TryGetDescriptor($"MulticastEffect<{intTypeName}>", out ConcreteEffectDescriptor? multicastInt) ||
            !TryGetDescriptor("RainbowEffect", out ConcreteEffectDescriptor? rainbow) ||
            !TryGetDescriptor("WhiteCircleEffect", out ConcreteEffectDescriptor? whiteCircle) ||
            !TryGetDescriptor("OverlayEffect", out ConcreteEffectDescriptor? overlay) ||
            !TryGetDescriptor("LedLineEffect", out ConcreteEffectDescriptor? ledLine) ||
            !TryGetDescriptor("TickerEffect", out ConcreteEffectDescriptor? ticker) ||
            !TryGetDescriptor("SinEffect", out ConcreteEffectDescriptor? sin) ||
            !TryGetDescriptor("MultiplyEffect", out ConcreteEffectDescriptor? multiply) ||
            !TryGetDescriptor("AddEffect", out ConcreteEffectDescriptor? add) ||
            !TryGetDescriptor("IntToDoubleEffect", out ConcreteEffectDescriptor? intToDouble))
        {
            return;
        }

        // Constants for dimensions
        NodeInstance<Payload> width = CreateNodeFromDescriptor(constantInt, new SKPoint(40, 40));
        width.Payload.EmbeddedInputs["value"].InvokeAndSetCurrentValue(14);
        NodeInstance<Payload> height = CreateNodeFromDescriptor(constantInt, new SKPoint(40, 160));
        height.Payload.EmbeddedInputs["value"].InvokeAndSetCurrentValue(14);
        NodeInstance<Payload> fps = CreateNodeFromDescriptor(constantInt, new SKPoint(40, 280));
        fps.Payload.EmbeddedInputs["value"].InvokeAndSetCurrentValue(24);

        // Multicast nodes to fan out width, height, fps to multiple consumers
        NodeInstance<Payload> widthMulticast = CreateNodeFromDescriptor(multicastInt, new SKPoint(160, 40));
        NodeInstance<Payload> heightMulticast = CreateNodeFromDescriptor(multicastInt, new SKPoint(160, 160));
        NodeInstance<Payload> fpsMulticast1 = CreateNodeFromDescriptor(multicastInt, new SKPoint(160, 280));
        NodeInstance<Payload>
            fpsMulticast2 = CreateNodeFromDescriptor(multicastInt, new SKPoint(160, 340)); // For ticker

        // Connect constants to multicast nodes
        NodesView.AddConnection(
            new PortReference(width.Id, "value", PortType.Output),
            new PortReference(widthMulticast.Id, "colorBuffer", PortType.Input));
        NodesView.AddConnection(
            new PortReference(height.Id, "value", PortType.Output),
            new PortReference(heightMulticast.Id, "colorBuffer", PortType.Input));
        NodesView.AddConnection(
            new PortReference(fps.Id, "value", PortType.Output),
            new PortReference(fpsMulticast1.Id, "colorBuffer", PortType.Input));
        // Chain fpsMulticast1.output1 -> fpsMulticast2 to get 3 outputs for fps
        NodesView.AddConnection(
            new PortReference(fpsMulticast1.Id, "output1", PortType.Output),
            new PortReference(fpsMulticast2.Id, "colorBuffer", PortType.Input));

        // Rainbow effect (background)
        NodeInstance<Payload> rainbowEffect = CreateNodeFromDescriptor(rainbow, new SKPoint(380, 120));

        // Connect dimensions to rainbow via multicast
        NodesView.AddConnection(
            new PortReference(widthMulticast.Id, "output0", PortType.Output),
            new PortReference(rainbowEffect.Id, "width", PortType.Input));
        NodesView.AddConnection(
            new PortReference(heightMulticast.Id, "output0", PortType.Output),
            new PortReference(rainbowEffect.Id, "height", PortType.Input));
        NodesView.AddConnection(
            new PortReference(fpsMulticast1.Id, "output0", PortType.Output),
            new PortReference(rainbowEffect.Id, "fps", PortType.Input));

        // White circle effect
        NodeInstance<Payload> circleEffect = CreateNodeFromDescriptor(whiteCircle, new SKPoint(380, 320));

        // Connect dimensions to circle via multicast
        NodesView.AddConnection(
            new PortReference(widthMulticast.Id, "output1", PortType.Output),
            new PortReference(circleEffect.Id, "width", PortType.Input));
        NodesView.AddConnection(
            new PortReference(heightMulticast.Id, "output1", PortType.Output),
            new PortReference(circleEffect.Id, "height", PortType.Input));
        NodesView.AddConnection(
            new PortReference(fpsMulticast2.Id, "output0", PortType.Output),
            new PortReference(circleEffect.Id, "fps", PortType.Input));

        // Ticker for animation (frame counter)
        NodeInstance<Payload> tickerEffect = CreateNodeFromDescriptor(ticker, new SKPoint(40, 420));
        NodesView.AddConnection(
            new PortReference(fpsMulticast2.Id, "output1", PortType.Output),
            new PortReference(tickerEffect.Id, "fps", PortType.Input));

        // Convert frame (int) to double
        NodeInstance<Payload> frameToDouble = CreateNodeFromDescriptor(intToDouble, new SKPoint(180, 420));
        NodesView.AddConnection(
            new PortReference(tickerEffect.Id, "frame", PortType.Output),
            new PortReference(frameToDouble.Id, "value", PortType.Input));

        // Multiply frame by speed factor (to control oscillation speed)
        NodeInstance<Payload> speedFactor = CreateNodeFromDescriptor(constantDouble, new SKPoint(40, 540));
        speedFactor.Payload.EmbeddedInputs["value"].InvokeAndSetCurrentValue(0.1);
        NodeInstance<Payload> speedMultiply = CreateNodeFromDescriptor(multiply, new SKPoint(320, 500));
        NodesView.AddConnection(
            new PortReference(frameToDouble.Id, "result", PortType.Output),
            new PortReference(speedMultiply.Id, "a", PortType.Input));
        NodesView.AddConnection(
            new PortReference(speedFactor.Id, "value", PortType.Output),
            new PortReference(speedMultiply.Id, "b", PortType.Input));

        // Apply sin function
        NodeInstance<Payload> sinEffect = CreateNodeFromDescriptor(sin, new SKPoint(460, 500));
        NodesView.AddConnection(
            new PortReference(speedMultiply.Id, "result", PortType.Output),
            new PortReference(sinEffect.Id, "value", PortType.Input));

        // Multiply by amplitude (radius range)
        NodeInstance<Payload> amplitude = CreateNodeFromDescriptor(constantDouble, new SKPoint(320, 620));
        amplitude.Payload.EmbeddedInputs["value"].InvokeAndSetCurrentValue(7 / 2.0 * Math.Sqrt(2));
        NodeInstance<Payload> amplitudeMultiply = CreateNodeFromDescriptor(multiply, new SKPoint(580, 540));
        NodesView.AddConnection(
            new PortReference(sinEffect.Id, "result", PortType.Output),
            new PortReference(amplitudeMultiply.Id, "a", PortType.Input));
        NodesView.AddConnection(
            new PortReference(amplitude.Id, "value", PortType.Output),
            new PortReference(amplitudeMultiply.Id, "b", PortType.Input));

        // Connect radius directly to circle (now takes double)
        NodesView.AddConnection(
            new PortReference(amplitudeMultiply.Id, "result", PortType.Output),
            new PortReference(circleEffect.Id, "radius", PortType.Input));

        // Overlay effect (combine rainbow + circle)
        NodeInstance<Payload> overlayEffect = CreateNodeFromDescriptor(overlay, new SKPoint(620, 220));
        NodesView.AddConnection(
            new PortReference(rainbowEffect.Id, "colorBuffer", PortType.Output),
            new PortReference(overlayEffect.Id, "colorBuffer0", PortType.Input));
        NodesView.AddConnection(
            new PortReference(circleEffect.Id, "colorBuffer", PortType.Output),
            new PortReference(overlayEffect.Id, "colorBuffer1", PortType.Input));

        // LED Line output
        NodeInstance<Payload> ledLineEffect = CreateNodeFromDescriptor(ledLine, new SKPoint(800, 220));
        NodesView.AddConnection(
            new PortReference(overlayEffect.Id, "colorBuffer", PortType.Output),
            new PortReference(ledLineEffect.Id, "colorBuffer", PortType.Input));
    }

    private string GetDefaultGraphPath()
    {
        string appDataPath = FileSystem.AppDataDirectory;
        return Path.Combine(appDataPath, "default_graph.json");
    }

    private void SaveGraphToFile(string filePath)
    {
        try
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            List<SerializableNode> nodes = [];
            foreach ((int nodeId, INodeInstance nodeInstance) in NodesView.Nodes)
            {
                if (nodeInstance is not NodeInstance<Payload> node) continue;

                List<SerializableEmbeddedInput> embeddedInputs = node.Payload.EmbeddedInputs
                    .Select(kvp => new SerializableEmbeddedInput(
                        kvp.Key,
                        kvp.Value.ValueType.Name,
                        kvp.Value.CurrentValue
                    ))
                    .ToList();

                nodes.Add(new SerializableNode(
                    nodeId,
                    node.Payload.TypeId,
                    node.Position.X,
                    node.Position.Y,
                    embeddedInputs
                ));
            }
            
            List<SerializableConnection> connections = [];
            foreach ((int fromNodeId, INodeInstance fromNode) in NodesView.Nodes)
            {
                foreach ((string fromPort, PortReference? toPort) in fromNode.Outputs)
                {
                    if (toPort is null) continue;

                    connections.Add(new SerializableConnection(
                        fromNodeId,
                        fromPort,
                        toPort.Value.NodeId,
                        toPort.Value.PortId
                    ));
                }
            }

            SerializableGraph serializableGraph = new(nodes, connections);
            
            JsonSerializerOptions options = new() { WriteIndented = true };
            string json = JsonSerializer.Serialize(serializableGraph, options);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save graph: {ex.Message}");
        }
    }

    private void LoadGraphFromFile(string filePath)
    {
        try
        {
            List<int> nodeIdsToRemove = NodesView.Nodes.Keys.ToList();
            foreach (int nodeId in nodeIdsToRemove)
            {
                NodesView.RemoveNode(nodeId);
            }

            _outputId = 0;

            string json = File.ReadAllText(filePath);
            SerializableGraph? serializableGraph = JsonSerializer.Deserialize<SerializableGraph>(json);

            if (serializableGraph == null)
                throw new InvalidOperationException("Failed to deserialize graph");
            
            Dictionary<int, NodeInstance<Payload>> nodeMap = new();

            // Recreate nodes
            foreach (SerializableNode serializedNode in serializableGraph.Nodes)
            {
                if (!TryGetDescriptor(serializedNode.TypeId, out ConcreteEffectDescriptor? descriptor))
                {
                    // Try to register generic effect if needed
                    if (serializedNode.TypeId.Contains("<") && serializedNode.TypeId.Contains(">"))
                    {
                        // Extract base type and type argument
                        int startIndex = serializedNode.TypeId.IndexOf('<');
                        int endIndex = serializedNode.TypeId.LastIndexOf('>');
                        string baseTypeId = serializedNode.TypeId.Substring(0, startIndex);
                        string typeArgStr = serializedNode.TypeId.Substring(startIndex + 1, endIndex - startIndex - 1);

                        // Try to resolve the type
                        Type? typeArg = Type.GetType($"System.{typeArgStr}");
                        if (typeArg != null)
                        {
                            EnsureGenericEffectRegistered(baseTypeId, typeArg);
                            if (!TryGetDescriptor(serializedNode.TypeId, out descriptor))
                                continue;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

                // Create node
                SKPoint position = new(serializedNode.PositionX, serializedNode.PositionY);
                NodeInstance<Payload> newNode = CreateNodeFromDescriptor(descriptor, position);
                nodeMap[serializedNode.NodeId] = newNode;

                // Restore embedded input values
                foreach (SerializableEmbeddedInput embeddedInput in serializedNode.EmbeddedInputs)
                {
                    if (embeddedInput.Value == null) continue;
                    
                    if (!newNode.Payload.EmbeddedInputs.TryGetValue(embeddedInput.Name,
                            out EmbeddedInputInfo? inputInfo)) continue;
                    
                    try
                    {
                        // Convert value to the correct type
                        object? convertedValue = ConvertValue(embeddedInput.Value, embeddedInput.ValueTypeName, inputInfo.ValueType);
                        if (convertedValue != null)
                        {
                            inputInfo.InvokeAndSetCurrentValue(convertedValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to convert value: {ex.Message}");
                    }
                }
            }

            // Restore connections
            foreach (SerializableConnection connection in serializableGraph.Connections)
            {
                if (nodeMap.TryGetValue(connection.FromNodeId, out NodeInstance<Payload>? fromNode) &&
                    nodeMap.TryGetValue(connection.ToNodeId, out NodeInstance<Payload>? toNode))
                {
                    NodesView.AddConnection(
                        new PortReference(fromNode.Id, connection.FromPort, PortType.Output),
                        new PortReference(toNode.Id, connection.ToPort, PortType.Input)
                    );
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load graph: {ex.Message}");
            throw;
        }
    }

    private object? ConvertValue(object? value, string valueTypeName, Type targetType)
    {
        if (value == null)
            return null;

        if (value.GetType() == targetType)
            return value;
        
        if (value is JsonElement jsonElement)
        {
            return valueTypeName switch
            {
                "Double" => jsonElement.GetDouble(),
                "Int32" => jsonElement.GetInt32(),
                _ => null,
            };
        }

        return null;
    }

    private void EnsureGenericEffectRegistered(string baseTypeId, params Type[] typeArguments)
    {
        GenericEffectDescriptor? genericDescriptor = EffectNodeCatalog.AllGeneric
            .FirstOrDefault(g => g.BaseTypeId == baseTypeId);

        if (genericDescriptor == null)
            return;

        ConcreteEffectDescriptor concreteDescriptor = genericDescriptor.MakeConcreteDescriptor(typeArguments);
        EffectNodeCatalog.RegisterConcreteDescriptor(concreteDescriptor);
    }


    private async void OnAddNodeClicked(object sender, EventArgs e)
    {
        NodePickerItem? selectedItem = GetSelectedNodePickerItem();
        if (selectedItem == null)
        {
            await DisplayAlertAsync("Add Node", "Select a node type first.", "OK");
            return;
        }

        ConcreteEffectDescriptor? descriptor;

        switch (selectedItem)
        {
            case ConcreteNodePickerItem concreteItem:
                descriptor = concreteItem.ConcreteDescriptor;
                break;

            case GenericNodePickerItem genericItem:
                // Get selected type argument
                if (GenericTypePicker.SelectedIndex < 0)
                {
                    await DisplayAlertAsync("Add Node", "Select a type parameter first.", "OK");
                    return;
                }

                AvailableTypeDescriptor selectedType =
                    EffectNodeCatalog.AvailableGenericTypes[GenericTypePicker.SelectedIndex];

                // Ensure the concrete version is registered
                EnsureGenericEffectRegistered(genericItem.GenericDescriptor.BaseTypeId, selectedType.Type);

                string typeId = $"{genericItem.GenericDescriptor.BaseTypeId}<{selectedType.Type.Name}>";

                if (!TryGetDescriptor(typeId, out descriptor))
                {
                    await DisplayAlertAsync("Add Node", "Failed to create generic effect.", "OK");
                    return;
                }

                break;

            default:
                await DisplayAlertAsync("Add Node", "Unknown node type.", "OK");
                return;
        }

        float offset = 30f * NodesView.Nodes.Count;
        SKPoint position = new(40f + offset % 240f, 80f + offset % 160f);
        CreateNodeFromDescriptor(descriptor, position);
    }

    private async void OnRemoveNodeClicked(object sender, EventArgs e)
    {
        if (NodesView.SelectedNodeId is not { } nodeId)
        {
            await DisplayAlertAsync("Remove Node", "Select a node to remove.", "OK");
            return;
        }

        NodesView.RemoveNode(nodeId);
    }

    private void OnFitViewClicked(object sender, EventArgs e) => NodesView.FitToContent();

    private async void OnCompileGraphClicked(object sender, EventArgs e)
    {
        EffectGraphModel model = BuildGraphModel();
        EffectGraphCompilationResult result = EffectGraphCompiler.Compile(model, new EffectContext());

        if (result.Success)
        {
            foreach (EffectGraphNode effectGraphNode in model.Nodes)
            {
                foreach ((string inputId, IUntypedOutputSlot outputSlot) in effectGraphNode.OutputsForEmbeddedInputs)
                {
                    // TODO: do something with this sh
                    if (inputId == "ledLine")
                    {
                        outputSlot.Invoke(_effectService.ConnectedLedLine);
                        continue;
                    }

                    if (effectGraphNode.EmbeddedInputValues.TryGetValue(inputId, out object? value))
                    {
                        outputSlot.Invoke(value);
                    }
                }
            }

            await DisplayAlertAsync("Compile Graph", "Graph compiled successfully.", "OK");
            return;
        }

        string errors = string.Join(Environment.NewLine, result.Errors);
        await DisplayAlertAsync("Compile Graph", errors, "OK");
    }

    private async void OnSaveGraphClicked(object sender, EventArgs e)
    {
        try
        {
            string filePath = GetDefaultGraphPath();
            SaveGraphToFile(filePath);
            await DisplayAlertAsync("Save Graph", "Graph saved successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Save Graph", $"Failed to save graph: {ex.Message}", "OK");
        }
    }

    private async void OnLoadGraphClicked(object sender, EventArgs e)
    {
        try
        {
            string filePath = GetDefaultGraphPath();
            if (!File.Exists(filePath))
            {
                await DisplayAlertAsync("Load Graph", "No saved graph found.", "OK");
                return;
            }

            LoadGraphFromFile(filePath);
            await DisplayAlertAsync("Load Graph", "Graph loaded successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Load Graph", $"Failed to load graph: {ex.Message}", "OK");
        }
    }

    private EffectGraphModel BuildGraphModel()
    {
        EffectGraphModel model = new();

        foreach (INodeInstance nodeInstance in NodesView.Nodes.Values)
        {
            if (nodeInstance is not NodeInstance<Payload> node) continue;

            Dictionary<string, IUntypedOutputSlot> outputsForEmbeddedInputs = node.Payload.EmbeddedInputs.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.OutputSlot
            );

            Dictionary<string, object?> currentValuesForEmbeddedInputs = node.Payload.EmbeddedInputs.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.CurrentValue
            );

            model.Nodes.Add(
                new EffectGraphNode(
                    node.Id,
                    node.Payload.TypeId,
                    outputsForEmbeddedInputs,
                    currentValuesForEmbeddedInputs
                )
            );
        }

        foreach ((int fromNodeId, INodeInstance fromNode) in NodesView.Nodes)
        {
            foreach ((string fromPort, PortReference? toPort) in fromNode.Outputs)
            {
                if (toPort is null) continue;

                model.Connections.Add(
                    new EffectGraphConnection(
                        fromNodeId,
                        fromPort,
                        toPort.Value.NodeId,
                        toPort.Value.PortId
                    )
                );
            }
        }

        return model;
    }

    private static bool TryGetDescriptor(string typeId, [MaybeNullWhen(false)] out ConcreteEffectDescriptor descriptor)
    {
        descriptor = EffectNodeCatalog.TryGet(typeId);
        return descriptor != null;
    }


    private void OnNodeSelectionChanged(object? sender, NodeSelectionChangedEventArgs e)
    {
        EmbeddedInputsContainer.Children.Clear();

        if (e.SelectedNode is not NodeInstance<Payload> node)
        {
            SelectedNodeLabel.Text = "Unsupported node type";
            EmbeddedInputsPanel.IsVisible = false;
            return;
        }

        SelectedNodeLabel.Text = node.Title;

        if (node.Payload.EmbeddedInputs.Count == 0)
        {
            EmbeddedInputsPanel.IsVisible = false;
            return;
        }

        foreach (EmbeddedInputInfo embeddedInput in node.Payload.EmbeddedInputs.Values)
        {
            View editor = CreateEditorForInput(embeddedInput);

            VerticalStackLayout inputLayout = new() { Spacing = 4 };
            inputLayout.Children.Add(new Label { Text = embeddedInput.Name, FontSize = 12, TextColor = Colors.Gray });
            inputLayout.Children.Add(editor);
            EmbeddedInputsContainer.Children.Add(inputLayout);
        }

        EmbeddedInputsPanel.IsVisible = EmbeddedInputsContainer.Children.Count > 0;
    }

    private View CreateEditorForInput(EmbeddedInputInfo inputInfo)
    {
        if (inputInfo.ValueType == typeof(int))
        {
            Entry entry = new()
            {
                Keyboard = Keyboard.Numeric,
                Text = inputInfo.CurrentValue?.ToString() ?? "0",
                Placeholder = "Enter integer value",
            };

            entry.TextChanged += (_, args) =>
            {
                if (int.TryParse(args.NewTextValue, out int intValue))
                {
                    inputInfo.InvokeAndSetCurrentValue(intValue);
                }
            };

            if (inputInfo.CurrentValue == null && int.TryParse(entry.Text, out int defaultValue))
            {
                inputInfo.InvokeAndSetCurrentValue(defaultValue);
            }

            return entry;
        }

        if (inputInfo.ValueType == typeof(float))
        {
            Entry entry = new()
            {
                Keyboard = Keyboard.Numeric,
                Text = inputInfo.CurrentValue?.ToString() ?? "0",
                Placeholder = "Enter float value",
            };

            entry.TextChanged += (_, args) =>
            {
                if (float.TryParse(args.NewTextValue, out float floatValue))
                {
                    inputInfo.InvokeAndSetCurrentValue(floatValue);
                }
            };

            return entry;
        }

        if (inputInfo.ValueType == typeof(double))
        {
            Entry entry = new()
            {
                Keyboard = Keyboard.Numeric,
                Text = inputInfo.CurrentValue?.ToString() ?? "0",
                Placeholder = "Enter double value",
            };

            entry.TextChanged += (_, args) =>
            {
                if (double.TryParse(args.NewTextValue, out double doubleValue))
                {
                    inputInfo.InvokeAndSetCurrentValue(doubleValue);
                }
            };

            return entry;
        }

        if (inputInfo.ValueType == typeof(string))
        {
            Entry entry = new() { Text = inputInfo.CurrentValue?.ToString() ?? "", Placeholder = "Enter text value" };

            entry.TextChanged += (_, args) =>
            {
                inputInfo.InvokeAndSetCurrentValue(args.NewTextValue);
            };

            return entry;
        }

        if (inputInfo.ValueType == typeof(bool))
        {
            Switch toggle = new() { IsToggled = inputInfo.CurrentValue is true };

            toggle.Toggled += (_, args) =>
            {
                inputInfo.InvokeAndSetCurrentValue(args.Value);
            };

            return toggle;
        }

        return new Label
        {
            Text = "(System provided)",
            FontSize = 12,
            TextColor = Colors.DimGray,
            FontAttributes = FontAttributes.Italic,
        };
    }
}
