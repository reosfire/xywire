using SkiaSharp;
using XywireHost.Core;
using XywireHost.Core.Graph;
using XywireHost.Core.services;
using XywireHost.UI.Controls;

namespace XywireHost.UI.Pages;

public partial class NodeEditorPage : ContentPage
{
    private readonly EffectService _effectService;
    
    private readonly IReadOnlyList<NodeDefinition> _definitions;
    private readonly Dictionary<string, NodeDefinition> _definitionsByTypeId;

    public NodeEditorPage(EffectService effectService)
    {
        _effectService = effectService;
        
        InitializeComponent();

        _definitions = EffectNodeCatalog.All
            .Select(descriptor => new NodeDefinition(
                descriptor.TypeId,
                descriptor.DisplayName,
                descriptor.Inputs,
                descriptor.Outputs))
            .ToList();

        _definitionsByTypeId = _definitions.ToDictionary(definition => definition.TypeId);

        NodePicker.ItemsSource = _definitions.Select(definition => definition.Name).ToList();
        if (NodePicker.ItemsSource.Count > 0)
        {
            NodePicker.SelectedIndex = 0;
        }
        
        SeedSampleGraph();
    }

    private void SeedSampleGraph()
    {
        if (!TryGetDefinition("ConstantInt", out NodeDefinition? constantInt) ||
            !TryGetDefinition("Rainbow", out NodeDefinition? rainbow))
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

    private async void OnAddNodeClicked(object sender, EventArgs e)
    {
        NodeDefinition? definition = GetSelectedDefinition();
        if (definition == null)
        {
            await DisplayAlert("Add Node", "Select a node type first.", "OK");
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

    private void OnFitViewClicked(object sender, EventArgs e)
    {
        NodesView.FitToContent();
    }

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
            // TODO probably rewriting can be started from here.
            object? data = node.TypeId switch
            {
                "ConstantInt" => 14,
                "LedLine" => _effectService.ConnectedLedLine,
                _ => null,
            };

            model.Nodes.Add(new EffectGraphNode(
                node.Id,
                node.TypeId,
                node.Position.X,
                node.Position.Y,
                data));
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

    private bool TryGetDefinition(string typeId, out NodeDefinition? definition) =>
        _definitionsByTypeId.TryGetValue(typeId, out definition);

    private NodeDefinition? GetSelectedDefinition()
    {
        if (NodePicker.SelectedIndex < 0 || NodePicker.SelectedIndex >= _definitions.Count)
        {
            return null;
        }

        return _definitions[NodePicker.SelectedIndex];
    }
}
