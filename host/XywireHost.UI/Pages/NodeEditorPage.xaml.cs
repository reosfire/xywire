using System.Diagnostics.CodeAnalysis;
using SkiaSharp;
using XywireHost.Core;
using XywireHost.Core.core;
using XywireHost.Core.Graph;
using XywireHost.Core.services;
using XywireHost.UI.Controls;

namespace XywireHost.UI.Pages;

public abstract record NodePickerItem(string DisplayName);
public sealed record ConcreteNodePickerItem(string DisplayName, ConcreteEffectDescriptor Prototype)
    : NodePickerItem(DisplayName);
public sealed record GenericNodePickerItem(string DisplayName, GenericEffectDescriptor GenericDescriptor)
    : NodePickerItem(DisplayName);

public record Payload(
    Dictionary<string, IUntypedOutputSlot> EmbeddedInputValues,
    Dictionary<string, EmbeddedInputInfo> EmbeddedInputs,
    string TypeId
);

public readonly record struct EmbeddedInputInfo(string Name, Type ValueType);

public partial class NodeEditorPage : ContentPage
{
    private readonly EffectService _effectService;

    private readonly List<NodePickerItem> _pickerItems = [];
    
    private readonly EffectOutputsCollection _systemOutputs = new();

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

    private int _outputId = 0;
    
    private readonly Dictionary<(int NodeId, string InputName), int> _nodeIdToOutputId = new();

    private NodeInstance<Payload> CreateNodeFromDescriptor(ConcreteEffectDescriptor prototype, SKPoint position)
    {
        NodeInstance<Payload> result = NodesView.AddNode(
            position: position,
            prototype.DisplayName,
            prototype.EmbeddedInputs.Select(e => e.Name).ToList(),
            prototype.Inputs.ToList(),
            prototype.Outputs.ToList(),
            new Payload(
                prototype.EmbeddedInputs.ToDictionary(e => e.Name, e => _systemOutputs.RegisterOutput((_outputId++).ToString(), e.ValueType)),
                prototype.EmbeddedInputs.ToDictionary(e => e.Name, e => new EmbeddedInputInfo(e.Name, e.ValueType)),
                prototype.TypeId
            )
        );

        int i = 0;
        foreach ((string name, Type type) in prototype.EmbeddedInputs)
        {
            _nodeIdToOutputId[(result.Id, name)] = _outputId - prototype.EmbeddedInputs.Count + i;
            i++;
        }

        return result;
    }

    private void SeedSampleGraph()
    {
        // First, ensure we have a ConstantEffect<Int32> in the catalog
        EnsureGenericEffectRegistered("ConstantEffect", typeof(int));

        // Use Type.Name for lookup (Int32, not int)
        string intTypeName = typeof(int).Name; // "Int32"
        if (!TryGetDefinition($"ConstantEffect<{intTypeName}>", out ConcreteEffectDescriptor? constantInt) ||
            !TryGetDefinition("RainbowEffect", out ConcreteEffectDescriptor? rainbow) ||
            !TryGetDefinition("WhiteCircleEffect", out ConcreteEffectDescriptor? whiteCircle) ||
            !TryGetDefinition("OverlayEffect", out ConcreteEffectDescriptor? overlay) ||
            !TryGetDefinition("LedLineEffect", out ConcreteEffectDescriptor? ledLine))
        {
            return;
        }

        NodeInstance<Payload> width = CreateNodeFromDescriptor(constantInt, new SKPoint(40, 40));
        NodeInstance<Payload> height = CreateNodeFromDescriptor(constantInt, new SKPoint(40, 160));
        NodeInstance<Payload> fps = CreateNodeFromDescriptor(constantInt, new SKPoint(40, 280));
        NodeInstance<Payload> output = CreateNodeFromDescriptor(rainbow, new SKPoint(320, 120));

        NodesView.AddConnection(
            new PortReference(width.Id, "value", PortType.Output),
            new PortReference(output.Id, "width", PortType.Input));
        NodesView.AddConnection(
            new PortReference(height.Id, "value", PortType.Output),
            new PortReference(output.Id, "height", PortType.Input));
        NodesView.AddConnection(
            new PortReference(fps.Id, "value", PortType.Output),
            new PortReference(output.Id, "fps", PortType.Input));
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
            await DisplayAlert("Add Node", "Select a node type first.", "OK");
            return;
        }

        ConcreteEffectDescriptor? definition;

        switch (selectedItem)
        {
            case ConcreteNodePickerItem concreteItem:
                definition = concreteItem.Prototype;
                break;

            case GenericNodePickerItem genericItem:
                // Get selected type argument
                if (GenericTypePicker.SelectedIndex < 0)
                {
                    await DisplayAlert("Add Node", "Select a type parameter first.", "OK");
                    return;
                }

                AvailableTypeDescriptor selectedType =
                    EffectNodeCatalog.AvailableGenericTypes[GenericTypePicker.SelectedIndex];

                // Ensure the concrete version is registered
                EnsureGenericEffectRegistered(genericItem.GenericDescriptor.BaseTypeId, selectedType.Type);

                string typeId = $"{genericItem.GenericDescriptor.BaseTypeId}<{selectedType.Type.Name}>";
                
                if (!TryGetDefinition(typeId, out definition))
                {
                    await DisplayAlert("Add Node", "Failed to create generic effect.", "OK");
                    return;
                }

                break;

            default:
                await DisplayAlert("Add Node", "Unknown node type.", "OK");
                return;
        }

        float offset = 30f * NodesView.Nodes.Count;
        SKPoint position = new(40f + offset % 240f, 80f + offset % 160f);
        CreateNodeFromDescriptor(definition, position);
    }

    private async void OnRemoveNodeClicked(object sender, EventArgs e)
    {
        if (NodesView.SelectedNodeId is not { } nodeId)
        {
            await DisplayAlert("Remove Node", "Select a node to remove.", "OK");
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
            foreach (((int nodeId, string inputName), object? value) in _systemOutputsCurrentValues)
            {
                _systemOutputs.Container[_nodeIdToOutputId[(nodeId, inputName)].ToString()].Invoke(value);
            }

            foreach (((int nodeId, string inputName), int outputId) in _nodeIdToOutputId)
            {
                if (inputName == "ledLine")
                {
                    _systemOutputs.Container[outputId.ToString()].Invoke(_effectService.ConnectedLedLine);
                }
            }
            
            await DisplayAlert("Compile Graph", "Graph compiled successfully.", "OK");
            return;
        }

        string errors = string.Join(Environment.NewLine, result.Errors);
        await DisplayAlert("Compile Graph", errors, "OK");
    }

    private EffectGraphModel BuildGraphModel()
    {
        EffectGraphModel model = new();

        foreach (NodeInstance<Payload> node in NodesView.Nodes.Values)
        {
            Dictionary<string, IUntypedOutputSlot> embeddedValues = new(node.Payload.EmbeddedInputValues);

            model.Nodes.Add(
                new EffectGraphNode(
                    node.Id,
                    node.Payload.TypeId,
                    node.Position.X,
                    node.Position.Y,
                    embeddedValues)
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

    private static bool TryGetDefinition(string typeId, [MaybeNullWhen(false)] out ConcreteEffectDescriptor definition)
    {
        definition = EffectNodeCatalog.TryGet(typeId);
        return definition != null;
    }


    private void OnNodeSelectionChanged(object? sender, NodeSelectionChangedEventArgs e)
    {
        EmbeddedInputsContainer.Children.Clear();

        if (e.SelectedNode == null || e.SelectedNode.EmbeddedInputs.Count == 0)
        {
            EmbeddedInputsPanel.IsVisible = false;
            return;
        }

        NodeInstance<Payload> node = e.SelectedNode as NodeInstance<Payload>;
        SelectedNodeLabel.Text = node.Title;

        foreach (EmbeddedInputInfo embeddedInput in node.Payload.EmbeddedInputs.Values)
        {
            View editor = CreateEditorForType(node, embeddedInput.ValueType, embeddedInput.Name);
            
            VerticalStackLayout inputLayout = new() { Spacing = 4 };
            inputLayout.Children.Add(new Label
            {
                Text = embeddedInput.Name, FontSize = 12, TextColor = Colors.Gray,
            });
            inputLayout.Children.Add(editor);
            EmbeddedInputsContainer.Children.Add(inputLayout);
        }

        EmbeddedInputsPanel.IsVisible = EmbeddedInputsContainer.Children.Count > 0;
    }
    
    private Dictionary<(int NodeId, string InputName), object?> _systemOutputsCurrentValues = new();

    private View CreateEditorForType(NodeInstance<Payload> node, Type valueType, string inputName)
    {
        // Get current value
        _systemOutputsCurrentValues.TryGetValue((node.Id, inputName), out object? currentValue);
        node.Payload.EmbeddedInputValues.TryGetValue(inputName, out IUntypedOutputSlot? currentSlot);

        if (valueType == typeof(int))
        {
            Entry entry = new()
            {
                Keyboard = Keyboard.Numeric,
                Text = currentValue?.ToString() ?? "0",
                Placeholder = "Enter integer value",
            };

            entry.TextChanged += (_, args) =>
            {
                if (int.TryParse(args.NewTextValue, out int intValue))
                {
                    (currentSlot as OutputSlot<int>).Invoke(intValue);
                    _systemOutputsCurrentValues[(node.Id, inputName)] = intValue;
                }
            };

            // Initialize with default value if not set
            if (currentValue == null && int.TryParse(entry.Text, out int defaultValue))
            {
                (currentSlot as OutputSlot<int>).Invoke(defaultValue);
                _systemOutputsCurrentValues[(node.Id, inputName)] = defaultValue;
            }

            return entry;
        }

        if (valueType == typeof(float))
        {
            Entry entry = new()
            {
                Keyboard = Keyboard.Numeric,
                Text = currentValue?.ToString() ?? "0",
                Placeholder = "Enter float value",
            };

            entry.TextChanged += (_, args) =>
            {
                if (float.TryParse(args.NewTextValue, out float floatValue))
                {
                    (currentSlot as OutputSlot<float>).Invoke(floatValue);
                    _systemOutputsCurrentValues[(node.Id, inputName)] = floatValue;
                }
            };

            return entry;
        }

        if (valueType == typeof(double))
        {
            Entry entry = new()
            {
                Keyboard = Keyboard.Numeric,
                Text = currentValue?.ToString() ?? "0",
                Placeholder = "Enter double value",
            };

            entry.TextChanged += (_, args) =>
            {
                if (double.TryParse(args.NewTextValue, out double doubleValue))
                {
                    (currentSlot as OutputSlot<double>).Invoke(doubleValue);
                    _systemOutputsCurrentValues[(node.Id, inputName)] = doubleValue;
                }
            };

            return entry;
        }

        if (valueType == typeof(string))
        {
            Entry entry = new() { Text = currentValue?.ToString() ?? "", Placeholder = "Enter text value" };

            entry.TextChanged += (_, args) =>
            {
                (currentSlot as OutputSlot<string>).Invoke(args.NewTextValue);
                _systemOutputsCurrentValues[(node.Id, inputName)] = args.NewTextValue;
            };

            return entry;
        }

        if (valueType == typeof(bool))
        {
            Switch toggle = new() { IsToggled = currentValue is true };

            toggle.Toggled += (_, args) =>
            {
                (currentSlot as OutputSlot<bool>).Invoke(args.Value);
                _systemOutputsCurrentValues[(node.Id, inputName)] = args.Value;
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
