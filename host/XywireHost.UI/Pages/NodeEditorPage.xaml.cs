using SkiaSharp;
using XywireHost.UI.Controls;

namespace XywireHost.UI.Pages;

public partial class NodeEditorPage : ContentPage
{
    private readonly IReadOnlyList<NodeDefinition> _definitions = new List<NodeDefinition>
    {
        new("Noise", [], ["Value"]),
        new("Color", ["Value"], ["Color"]),
        new("Blend", ["A", "B", "Factor"], ["Out"]),
        new("Time", [], ["Seconds"]),
        new("Output", ["Color"], []),
    };

    public NodeEditorPage()
    {
        InitializeComponent();

        NodePicker.ItemsSource = _definitions.Select(definition => definition.Name).ToList();
        if (NodePicker.ItemsSource.Count > 0)
        {
            NodePicker.SelectedIndex = 0;
        }
        
        SeedSampleGraph();
    }

    private void SeedSampleGraph()
    {
        NodeInstance noise = NodesView.AddNode(_definitions[0], new SKPoint(40, 40));
        NodeInstance color = NodesView.AddNode(_definitions[1], new SKPoint(280, 40));
        NodeInstance output = NodesView.AddNode(_definitions[4], new SKPoint(520, 60));

        NodesView.TryAddConnection(
            new NodePortReference(noise.Id, "Value", false),
            new NodePortReference(color.Id, "Value", true));
        NodesView.TryAddConnection(
            new NodePortReference(color.Id, "Color", false),
            new NodePortReference(output.Id, "Color", true));
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

    private NodeDefinition? GetSelectedDefinition()
    {
        if (NodePicker.SelectedIndex < 0 || NodePicker.SelectedIndex >= _definitions.Count)
        {
            return null;
        }

        return _definitions[NodePicker.SelectedIndex];
    }
}
