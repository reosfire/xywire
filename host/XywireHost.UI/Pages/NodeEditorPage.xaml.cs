using SkiaSharp;
using XywireHost.UI.Controls;

namespace XywireHost.UI.Pages;

public partial class NodeEditorPage : ContentPage
{
    private readonly IReadOnlyList<NodeDefinition> _definitions = new List<NodeDefinition>
    {
        new("Noise", Array.Empty<string>(), new[] { "Value" }),
        new("Color", new[] { "Value" }, new[] { "Color" }),
        new("Blend", new[] { "A", "B", "Factor" }, new[] { "Out" }),
        new("Time", Array.Empty<string>(), new[] { "Seconds" }),
        new("Output", new[] { "Color" }, Array.Empty<string>())
    };

    public NodeEditorPage()
    {
        InitializeComponent();

        NodePicker.ItemsSource = _definitions.Select(definition => definition.Name).ToList();
        if (NodePicker.ItemsSource.Count > 0)
        {
            NodePicker.SelectedIndex = 0;
        }

        NodesView.SelectionChanged += OnSelectionChanged;
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
        SKPoint position = new(40f + (offset % 240f), 80f + (offset % 160f));
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

    private async void OnConnectClicked(object sender, EventArgs e)
    {
        if (NodesView.SelectedOutput is not { } output || NodesView.SelectedInput is not { } input)
        {
            await DisplayAlert("Connect", "Select an output port and an input port.", "OK");
            return;
        }

        bool added = NodesView.TryAddConnection(output, input);
        if (!added)
        {
            await DisplayAlert("Connect", "Connection already exists or is invalid.", "OK");
        }
    }

    private async void OnDisconnectClicked(object sender, EventArgs e)
    {
        if (NodesView.SelectedOutput is not { } output || NodesView.SelectedInput is not { } input)
        {
            await DisplayAlert("Disconnect", "Select an output port and an input port.", "OK");
            return;
        }

        bool removed = NodesView.RemoveConnection(output, input);
        if (!removed)
        {
            await DisplayAlert("Disconnect", "No matching connection found.", "OK");
        }
    }

    private void OnClearSelectionClicked(object sender, EventArgs e)
    {
        NodesView.ClearSelection();
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        string nodeText = NodesView.SelectedNodeId is { } id ? id.ToString() : "none";
        string outputText = NodesView.SelectedOutput is { } output ? output.PortName : "none";
        string inputText = NodesView.SelectedInput is { } input ? input.PortName : "none";
        SelectionLabel.Text = $"Selected node: {nodeText}, output: {outputText}, input: {inputText}";
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
