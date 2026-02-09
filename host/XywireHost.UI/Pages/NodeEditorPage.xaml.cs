using System.Diagnostics.CodeAnalysis;
using SkiaSharp;
using XywireHost.Core;
using XywireHost.Core.Graph;
using XywireHost.Core.services;
using XywireHost.UI.Controls;

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
) {
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
            position: position,
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
        EnsureGenericEffectRegistered("ConstantEffect", typeof(int));
        
        string intTypeName = typeof(int).Name; // "Int32"
        if (!TryGetDescriptor($"ConstantEffect<{intTypeName}>", out ConcreteEffectDescriptor? constantInt) ||
            !TryGetDescriptor("RainbowEffect", out ConcreteEffectDescriptor? rainbow) ||
            !TryGetDescriptor("WhiteCircleEffect", out ConcreteEffectDescriptor? whiteCircle) ||
            !TryGetDescriptor("OverlayEffect", out ConcreteEffectDescriptor? overlay) ||
            !TryGetDescriptor("LedLineEffect", out ConcreteEffectDescriptor? ledLine))
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
            inputLayout.Children.Add(new Label { Text = embeddedInput.Name, FontSize = 12, TextColor = Colors.Gray, });
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
