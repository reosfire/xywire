using System.Diagnostics.CodeAnalysis;
using SkiaSharp;
using XywireHost.Core;
using XywireHost.Core.core;
using XywireHost.Core.Graph;
using XywireHost.Core.services;
using XywireHost.UI.Controls;

namespace XywireHost.UI.Pages;

public abstract record NodePickerItem(string DisplayName);
public sealed record ConcreteNodePickerItem(string DisplayName, NodeDefinition Definition) : NodePickerItem(DisplayName);
public sealed record GenericNodePickerItem(string DisplayName, GenericEffectDescriptor GenericDescriptor) : NodePickerItem(DisplayName);

public partial class NodeEditorPage : ContentPage
{
    private readonly EffectService _effectService;

    private readonly List<NodePickerItem> _pickerItems = [];
    private readonly Dictionary<string, NodeDefinition> _definitionsByTypeId = new();

    public NodeEditorPage(EffectService effectService)
    {
        InitializeComponent();

        _effectService = effectService;
        
        foreach (EffectNodeDescriptor descriptor in EffectNodeCatalog.All)
        {
            NodeDefinition definition = DescriptorToDefinition(descriptor);
            _definitionsByTypeId[definition.TypeId] = definition;
            _pickerItems.Add(new ConcreteNodePickerItem(descriptor.DisplayName, definition));
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

    private static NodeDefinition DescriptorToDefinition(EffectNodeDescriptor descriptor)
    {
        return new NodeDefinition(
            descriptor.TypeId,
            descriptor.DisplayName,
            descriptor.Inputs,
            descriptor.Outputs,
            descriptor.EmbeddedInputs
                .Select(e => new EmbeddedInputInfo(e.Name, e.ValueType))
                .ToList());
    }

    private void OnNodePickerSelectionChanged(object? sender, EventArgs e)
    {
        NodePickerItem? selectedItem = GetSelectedPickerItem();
        GenericTypePicker.IsVisible = selectedItem is GenericNodePickerItem;
    }

    private NodePickerItem? GetSelectedPickerItem()
    {
        if (NodePicker.SelectedIndex < 0 || NodePicker.SelectedIndex >= _pickerItems.Count)
            return null;
        return _pickerItems[NodePicker.SelectedIndex];
    }

    private void SeedSampleGraph()
    {
        // First, ensure we have a ConstantEffect<Int32> in the catalog
        EnsureGenericEffectRegistered("ConstantEffect", typeof(int));
        
        // Use Type.Name for lookup (Int32, not int)
        string intTypeName = typeof(int).Name; // "Int32"
        if (!TryGetDefinition($"ConstantEffect<{intTypeName}>", out NodeDefinition? constantInt) ||
            !TryGetDefinition("RainbowEffect", out NodeDefinition? rainbow) ||
            !TryGetDefinition("WhiteCircleEffect", out NodeDefinition? whiteCircle) ||
            !TryGetDefinition("OverlayEffect", out NodeDefinition? overlay) ||
            !TryGetDefinition("LedLineEffect", out NodeDefinition? ledLine))
        {
            return;
        }

        NodeInstance width = NodesView.AddNode(constantInt, new SKPoint(40, 40));
        NodeInstance height = NodesView.AddNode(constantInt, new SKPoint(40, 160));
        NodeInstance fps = NodesView.AddNode(constantInt, new SKPoint(40, 280));
        NodeInstance output = NodesView.AddNode(rainbow, new SKPoint(320, 120));

        NodesView.TryAddConnection(
            new NodePortReference(width.Id, "value", false),
            new NodePortReference(output.Id, "width", true));
        NodesView.TryAddConnection(
            new NodePortReference(height.Id, "value", false),
            new NodePortReference(output.Id, "height", true));
        NodesView.TryAddConnection(
            new NodePortReference(fps.Id, "value", false),
            new NodePortReference(output.Id, "fps", true));
    }
    
    private void EnsureGenericEffectRegistered(string baseTypeId, params Type[] typeArguments)
    {
        string typeArgsString = string.Join(", ", typeArguments.Select(t => t.Name));
        string typeId = $"{baseTypeId}<{typeArgsString}>";
        
        if (_definitionsByTypeId.ContainsKey(typeId))
            return;
        
        GenericEffectDescriptor? genericDescriptor = EffectNodeCatalog.AllGeneric
            .FirstOrDefault(g => g.BaseTypeId == baseTypeId);

        if (genericDescriptor == null)
            return;
        
        EffectNodeDescriptor concreteDescriptor = genericDescriptor.MakeConcreteDescriptor(typeArguments);
        EffectNodeCatalog.RegisterConcreteDescriptor(concreteDescriptor);

        NodeDefinition definition = DescriptorToDefinition(concreteDescriptor);
        _definitionsByTypeId[definition.TypeId] = definition;
    }

    private async void OnAddNodeClicked(object sender, EventArgs e)
    {
        NodePickerItem? selectedItem = GetSelectedPickerItem();
        if (selectedItem == null)
        {
            await DisplayAlert("Add Node", "Select a node type first.", "OK");
            return;
        }

        NodeDefinition? definition;

        switch (selectedItem)
        {
            case ConcreteNodePickerItem concreteItem:
                definition = concreteItem.Definition;
                break;

            case GenericNodePickerItem genericItem:
                // Get selected type argument
                if (GenericTypePicker.SelectedIndex < 0)
                {
                    await DisplayAlert("Add Node", "Select a type parameter first.", "OK");
                    return;
                }

                AvailableTypeDescriptor selectedType = EffectNodeCatalog.AvailableGenericTypes[GenericTypePicker.SelectedIndex];
                
                // Ensure the concrete version is registered
                EnsureGenericEffectRegistered(genericItem.GenericDescriptor.BaseTypeId, selectedType.Type);
                
                string typeId = $"{genericItem.GenericDescriptor.BaseTypeId}<{selectedType.Type.Name}>";
                if (!_definitionsByTypeId.TryGetValue(typeId, out definition))
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
        NodesView.AddNode(definition, position);
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
            await DisplayAlert("Compile Graph", "Graph compiled successfully.", "OK");
            return;
        }

        string errors = string.Join(Environment.NewLine, result.Errors);
        await DisplayAlert("Compile Graph", errors, "OK");
    }

    private EffectGraphModel BuildGraphModel()
    {
        EffectGraphModel model = new();

        foreach (NodeInstance node in NodesView.Nodes)
        {
            Dictionary<string, object?>? embeddedValues = null;

            if (node.EmbeddedInputs.Count > 0)
            {
                embeddedValues = new Dictionary<string, object?>(node.EmbeddedInputValues);
                
                foreach (EmbeddedInputInfo embeddedInput in node.EmbeddedInputs)
                {
                    if (embeddedInput.ValueType == typeof(LedLine) &&
                        !embeddedValues.ContainsKey(embeddedInput.Name))
                    {
                        embeddedValues[embeddedInput.Name] = _effectService.ConnectedLedLine;
                    }
                }
            }

            model.Nodes.Add(new EffectGraphNode(
                node.Id,
                node.TypeId,
                node.Position.X,
                node.Position.Y,
                embeddedValues));
        }

        foreach (NodeConnection connection in NodesView.Connections)
        {
            model.Connections.Add(new EffectGraphConnection(
                connection.FromNodeId,
                connection.FromPort,
                connection.ToNodeId,
                connection.ToPort));
        }

        return model;
    }

    private bool TryGetDefinition(string typeId, [MaybeNullWhen(false)] out NodeDefinition definition) =>
        _definitionsByTypeId.TryGetValue(typeId, out definition);


    private void OnNodeSelectionChanged(object? sender, NodeSelectionChangedEventArgs e)
    {
        EmbeddedInputsContainer.Children.Clear();

        if (e.SelectedNode == null || e.SelectedNode.EmbeddedInputs.Count == 0)
        {
            EmbeddedInputsPanel.IsVisible = false;
            return;
        }

        NodeInstance node = e.SelectedNode;
        SelectedNodeLabel.Text = node.Title;

        foreach (EmbeddedInputInfo embeddedInput in node.EmbeddedInputs)
        {
            View? editor = CreateEditorForType(node, embeddedInput);
            if (editor != null)
            {
                VerticalStackLayout inputLayout = new() { Spacing = 4 };
                inputLayout.Children.Add(new Label
                {
                    Text = embeddedInput.Name, FontSize = 12, TextColor = Colors.Gray,
                });
                inputLayout.Children.Add(editor);
                EmbeddedInputsContainer.Children.Add(inputLayout);
            }
        }

        EmbeddedInputsPanel.IsVisible = EmbeddedInputsContainer.Children.Count > 0;
    }

    private View? CreateEditorForType(NodeInstance node, EmbeddedInputInfo embeddedInput)
    {
        Type valueType = embeddedInput.ValueType;
        string inputName = embeddedInput.Name;

        // Get current value
        node.EmbeddedInputValues.TryGetValue(inputName, out object? currentValue);

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
                    node.EmbeddedInputValues[inputName] = intValue;
                }
            };

            // Initialize with default value if not set
            if (currentValue == null && int.TryParse(entry.Text, out int defaultValue))
            {
                node.EmbeddedInputValues[inputName] = defaultValue;
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
                    node.EmbeddedInputValues[inputName] = floatValue;
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
                    node.EmbeddedInputValues[inputName] = doubleValue;
                }
            };

            return entry;
        }

        if (valueType == typeof(string))
        {
            Entry entry = new() { Text = currentValue?.ToString() ?? "", Placeholder = "Enter text value" };

            entry.TextChanged += (_, args) =>
            {
                node.EmbeddedInputValues[inputName] = args.NewTextValue;
            };

            return entry;
        }

        if (valueType == typeof(bool))
        {
            Switch toggle = new() { IsToggled = currentValue is true };

            toggle.Toggled += (_, args) =>
            {
                node.EmbeddedInputValues[inputName] = args.Value;
            };

            return toggle;
        }

        // For complex types like LedLine, show a label indicating it's system-provided
        return new Label
        {
            Text = "(System provided)",
            FontSize = 12,
            TextColor = Colors.DimGray,
            FontAttributes = FontAttributes.Italic,
        };
    }
}
