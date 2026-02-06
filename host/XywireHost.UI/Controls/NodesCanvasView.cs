using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace XywireHost.UI.Controls;

public sealed class NodesCanvasView : SKCanvasView
{
    private const float NodePadding = 10f;
    private const float PortRadius = 5f;
    private const float HeaderHeight = 22f;
    private const float LineHeight = 18f;
    private const float MinNodeWidth = 150f;
    private const float MinZoom = 0.2f;
    private const float MaxZoom = 3.0f;

    private readonly Dictionary<Guid, NodeLayout> _layouts = new();

    private readonly SKPaint _titlePaint = new() { Color = SKColors.White, TextSize = 15f, IsAntialias = true };

    private readonly SKPaint _textPaint = new()
    {
        Color = new SKColor(220, 220, 220), TextSize = 13f, IsAntialias = true,
    };

    private readonly SKPaint _nodePaint = new() { Color = new SKColor(40, 40, 40), IsAntialias = true };

    private readonly SKPaint _selectedNodePaint = new() { Color = new SKColor(60, 60, 60), IsAntialias = true };

    private readonly SKPaint _nodeStrokePaint = new()
    {
        Color = new SKColor(90, 90, 90), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f,
    };

    private readonly SKPaint _portPaint = new() { Color = new SKColor(160, 200, 255), IsAntialias = true };

    private readonly SKPaint _connectionPaint = new()
    {
        Color = new SKColor(140, 180, 255), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f,
    };

    private NodeDragState? _dragState;
    private PanDragState? _panState;
    private ConnectionDragState? _connectionDrag;
    private SKPoint _cameraOffset = new(0f, 0f);
    private float _zoom = 1f;

    public NodesCanvasView()
    {
        EnableTouchEvents = true;
        Touch += OnTouch;
    }

    public IList<NodeInstance> Nodes { get; } = new List<NodeInstance>();
    public IList<NodeConnection> Connections { get; } = new List<NodeConnection>();

    public NodePortReference? SelectedOutput { get; private set; }
    public NodePortReference? SelectedInput { get; private set; }
    public Guid? SelectedNodeId { get; private set; }

    public event EventHandler? SelectionChanged;

    public NodeInstance AddNode(NodeDefinition definition, SKPoint position)
    {
        NodeInstance node = new(definition, position);
        Nodes.Add(node);
        SelectedNodeId = node.Id;
        InvalidateSurface();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        return node;
    }

    public void RemoveNode(Guid nodeId)
    {
        NodeInstance? node = Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null)
        {
            return;
        }

        Nodes.Remove(node);
        for (int i = Connections.Count - 1; i >= 0; i--)
        {
            NodeConnection connection = Connections[i];
            if (connection.FromNodeId == nodeId || connection.ToNodeId == nodeId)
            {
                Connections.RemoveAt(i);
            }
        }

        ClearSelection();
        InvalidateSurface();
    }

    public bool TryAddConnection(NodePortReference output, NodePortReference input)
    {
        if (output.IsInput || !input.IsInput)
        {
            return false;
        }

        bool exists = Connections.Any(connection =>
            connection.FromNodeId == output.NodeId &&
            connection.FromPort == output.PortName &&
            connection.ToNodeId == input.NodeId &&
            connection.ToPort == input.PortName);

        if (exists)
        {
            return false;
        }

        Connections.Add(new NodeConnection(output.NodeId, output.PortName, input.NodeId, input.PortName));
        InvalidateSurface();
        return true;
    }

    public bool RemoveConnection(NodePortReference output, NodePortReference input)
    {
        for (int i = Connections.Count - 1; i >= 0; i--)
        {
            NodeConnection connection = Connections[i];
            if (connection.FromNodeId == output.NodeId &&
                connection.FromPort == output.PortName &&
                connection.ToNodeId == input.NodeId &&
                connection.ToPort == input.PortName)
            {
                Connections.RemoveAt(i);
                InvalidateSurface();
                return true;
            }
        }

        return false;
    }

    public void ClearSelection()
    {
        SelectedOutput = null;
        SelectedInput = null;
        SelectedNodeId = null;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
    }

    public void FitToContent(float padding = 40f)
    {
        if (Nodes.Count == 0 || CanvasSize.Width <= 0f || CanvasSize.Height <= 0f)
        {
            return;
        }

        SKRect? bounds = null;
        foreach (NodeInstance node in Nodes)
        {
            SKRect nodeBounds = BuildLayout(node).Bounds;
            bounds = bounds.HasValue ? SKRect.Union(bounds.Value, nodeBounds) : nodeBounds;
        }

        if (!bounds.HasValue || bounds.Value.Width <= 0f || bounds.Value.Height <= 0f)
        {
            return;
        }

        SKRect world = bounds.Value;
        world.Inflate(padding, padding);

        float zoomX = CanvasSize.Width / world.Width;
        float zoomY = CanvasSize.Height / world.Height;
        float targetZoom = Clamp(Math.Min(zoomX, zoomY), MinZoom, MaxZoom);

        SKPoint worldCenter = new(world.MidX, world.MidY);
        SKPoint screenCenter = new(CanvasSize.Width / 2f, CanvasSize.Height / 2f);
        _zoom = targetZoom;
        _cameraOffset = new SKPoint(screenCenter.X / _zoom - worldCenter.X, screenCenter.Y / _zoom - worldCenter.Y);

        InvalidateSurface();
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        SKCanvas canvas = e.Surface.Canvas;
        canvas.Clear(new SKColor(24, 24, 24));

        canvas.Scale(_zoom);
        canvas.Translate(_cameraOffset.X, _cameraOffset.Y);

        _layouts.Clear();

        foreach (NodeInstance node in Nodes)
        {
            NodeLayout layout = BuildLayout(node);
            _layouts[node.Id] = layout;

            bool isSelected = SelectedNodeId == node.Id;
            SKPaint fillPaint = isSelected ? _selectedNodePaint : _nodePaint;

            canvas.DrawRoundRect(layout.Bounds, 8, 8, fillPaint);
            canvas.DrawRoundRect(layout.Bounds, 8, 8, _nodeStrokePaint);

            float titleBaseline = layout.Bounds.Top + NodePadding + HeaderHeight;
            canvas.DrawText(node.Title, layout.Bounds.Left + NodePadding, titleBaseline, _titlePaint);
        }

        foreach (NodeConnection connection in Connections)
        {
            if (!_layouts.TryGetValue(connection.FromNodeId, out NodeLayout? fromLayout) ||
                !_layouts.TryGetValue(connection.ToNodeId, out NodeLayout? toLayout))
            {
                continue;
            }

            if (!fromLayout.OutputPorts.TryGetValue(connection.FromPort, out SKPoint start) ||
                !toLayout.InputPorts.TryGetValue(connection.ToPort, out SKPoint end))
            {
                continue;
            }

            DrawConnection(canvas, start, end);
        }

        if (_connectionDrag is { } drag && TryGetPortPosition(drag.StartPort, out SKPoint startPosition))
        {
            DrawConnection(canvas, startPosition, drag.CurrentWorld);
        }

        foreach (NodeInstance node in Nodes)
        {
            NodeLayout layout = _layouts[node.Id];
            
            foreach ((string portName, SKPoint position) in layout.InputPorts)
            {
                DrawPort(canvas, position, SelectedInput, node.Id, portName, true);
                canvas.DrawText(portName, position.X + 10f, position.Y + 5f, _textPaint);
            }

            foreach ((string portName, SKPoint position) in layout.OutputPorts)
            {
                DrawPort(canvas, position, SelectedOutput, node.Id, portName, false);
                float textWidth = _textPaint.MeasureText(portName);
                canvas.DrawText(portName, position.X - 10f - textWidth, position.Y + 5f, _textPaint);
            }
        }
    }

    private NodeLayout BuildLayout(NodeInstance node)
    {
        float titleWidth = _titlePaint.MeasureText(node.Title);
        float maxInputWidth = node.Inputs.Count == 0 ? 0f : node.Inputs.Max(input => _textPaint.MeasureText(input));
        float maxOutputWidth =
            node.Outputs.Count == 0 ? 0f : node.Outputs.Max(output => _textPaint.MeasureText(output));

        float leftColumnWidth = Math.Max(60f, maxInputWidth + 16f);
        float rightColumnWidth = Math.Max(60f, maxOutputWidth + 16f);
        float width = Math.Max(MinNodeWidth, NodePadding * 2 + leftColumnWidth + rightColumnWidth + 20f);
        width = Math.Max(width, NodePadding * 2 + titleWidth + 10f);

        int rowCount = Math.Max(node.Inputs.Count, node.Outputs.Count);
        float height = NodePadding * 2 + HeaderHeight + rowCount * LineHeight;

        SKRect bounds = new(node.Position.X, node.Position.Y, node.Position.X + width, node.Position.Y + height);
        Dictionary<string, SKPoint> inputPorts = new();
        Dictionary<string, SKPoint> outputPorts = new();

        float rowStart = bounds.Top + NodePadding + HeaderHeight + LineHeight / 2f;
        for (int i = 0; i < node.Inputs.Count; i++)
        {
            float y = rowStart + i * LineHeight;
            inputPorts[node.Inputs[i]] = new SKPoint(bounds.Left + NodePadding, y);
        }

        for (int i = 0; i < node.Outputs.Count; i++)
        {
            float y = rowStart + i * LineHeight;
            outputPorts[node.Outputs[i]] = new SKPoint(bounds.Right - NodePadding, y);
        }

        return new NodeLayout(bounds, inputPorts, outputPorts);
    }

    private void DrawPort(SKCanvas canvas, SKPoint position, NodePortReference? selectedPort, Guid nodeId,
        string portName, bool isInput)
    {
        float radius = PortRadius;
        if (selectedPort is { } selected && selected.NodeId == nodeId && selected.PortName == portName &&
            selected.IsInput == isInput)
        {
            radius += 2f;
        }

        canvas.DrawCircle(position, radius, _portPaint);
    }

    private void DrawConnection(SKCanvas canvas, SKPoint start, SKPoint end)
    {
        float distance = Math.Abs(end.X - start.X) * 0.5f;
        float controlOffset = Math.Max(distance, 40f);

        using SKPath path = new();
        path.MoveTo(start);
        path.CubicTo(start.X + controlOffset, start.Y, end.X - controlOffset, end.Y, end.X, end.Y);
        canvas.DrawPath(path, _connectionPaint);
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        if (e.ActionType == SKTouchAction.WheelChanged)
        {
            float zoomFactor = (float)Math.Pow(1.1f, e.WheelDelta / 120f);
            float newZoom = Clamp(_zoom * zoomFactor, MinZoom, MaxZoom);
            if (Math.Abs(newZoom - _zoom) > 0.0001f)
            {
                SKPoint worldBefore = ScreenToWorld(e.Location);
                _zoom = newZoom;
                _cameraOffset = new SKPoint(e.Location.X / _zoom - worldBefore.X, e.Location.Y / _zoom - worldBefore.Y);
                InvalidateSurface();
            }

            e.Handled = true;
            return;
        }

        if (e.ActionType == SKTouchAction.Pressed)
        {
            SKPoint worldLocation = ScreenToWorld(e.Location);
            if (TryHitPort(worldLocation, out NodePortReference port))
            {
                if (port.IsInput)
                {
                    SelectedInput = port;
                }
                else
                {
                    SelectedOutput = port;
                }

                SelectedNodeId = port.NodeId;
                _dragState = null;
                _panState = null;
                _connectionDrag = new ConnectionDragState(port, worldLocation);
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                InvalidateSurface();
                e.Handled = true;
                return;
            }

            if (TryHitNode(worldLocation, out Guid nodeId, out SKPoint nodeOrigin))
            {
                SelectedNodeId = nodeId;
                _dragState = new NodeDragState(nodeId, worldLocation - nodeOrigin);
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                InvalidateSurface();
                e.Handled = true;
                return;
            }

            ClearSelection();
            _panState = new PanDragState(e.Location, _cameraOffset);
            e.Handled = true;
            return;
        }

        if (e.ActionType == SKTouchAction.Moved && _connectionDrag is { } drag)
        {
            _connectionDrag = drag with { CurrentWorld = ScreenToWorld(e.Location) };
            InvalidateSurface();
            e.Handled = true;
            return;
        }

        if (e.ActionType == SKTouchAction.Moved && _dragState is { } dragNode)
        {
            NodeInstance? node = Nodes.FirstOrDefault(n => n.Id == dragNode.NodeId);
            if (node != null)
            {
                node.Position = ScreenToWorld(e.Location) - dragNode.Offset;
                InvalidateSurface();
            }

            e.Handled = true;
            return;
        }

        if (e.ActionType == SKTouchAction.Moved && _panState is { } pan)
        {
            SKPoint deltaScreen = e.Location - pan.StartScreen;
            _cameraOffset = pan.StartOffset + new SKPoint(deltaScreen.X / _zoom, deltaScreen.Y / _zoom);
            InvalidateSurface();
            e.Handled = true;
            return;
        }

        if ((e.ActionType == SKTouchAction.Released || e.ActionType == SKTouchAction.Cancelled) &&
            _connectionDrag is { } releasedDrag)
        {
            SKPoint worldLocation = ScreenToWorld(e.Location);
            if (TryHitPort(worldLocation, out NodePortReference targetPort))
            {
                ToggleConnection(releasedDrag.StartPort, targetPort);
            }

            _connectionDrag = null;
            e.Handled = true;
            return;
        }

        if (e.ActionType == SKTouchAction.Released || e.ActionType == SKTouchAction.Cancelled)
        {
            _dragState = null;
            _panState = null;
            e.Handled = true;
        }
    }

    private bool TryHitNode(SKPoint location, out Guid nodeId, out SKPoint nodeOrigin)
    {
        foreach ((Guid id, NodeLayout layout) in _layouts)
        {
            if (layout.Bounds.Contains(location))
            {
                nodeId = id;
                nodeOrigin = new SKPoint(layout.Bounds.Left, layout.Bounds.Top);
                return true;
            }
        }

        nodeId = Guid.Empty;
        nodeOrigin = default;
        return false;
    }

    private bool TryHitPort(SKPoint location, out NodePortReference port)
    {
        foreach ((Guid id, NodeLayout layout) in _layouts)
        {
            foreach ((string portName, SKPoint position) in layout.InputPorts)
            {
                if (IsNear(location, position))
                {
                    port = new NodePortReference(id, portName, true);
                    return true;
                }
            }

            foreach ((string portName, SKPoint position) in layout.OutputPorts)
            {
                if (IsNear(location, position))
                {
                    port = new NodePortReference(id, portName, false);
                    return true;
                }
            }
        }

        port = default;
        return false;
    }

    private bool ToggleConnection(NodePortReference a, NodePortReference b)
    {
        if (a.NodeId == b.NodeId && a.PortName == b.PortName && a.IsInput == b.IsInput)
        {
            return false;
        }

        NodePortReference output = a.IsInput ? b : a;
        NodePortReference input = a.IsInput ? a : b;

        if (output.IsInput || !input.IsInput)
        {
            return false;
        }

        if (RemoveConnection(output, input))
        {
            return true;
        }

        return TryAddConnection(output, input);
    }

    private bool TryGetPortPosition(NodePortReference port, out SKPoint position)
    {
        if (_layouts.TryGetValue(port.NodeId, out NodeLayout? layout))
        {
            Dictionary<string, SKPoint> ports = port.IsInput ? layout.InputPorts : layout.OutputPorts;
            if (ports.TryGetValue(port.PortName, out position))
            {
                return true;
            }
        }

        position = default;
        return false;
    }

    private static bool IsNear(SKPoint a, SKPoint b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return dx * dx + dy * dy <= (PortRadius + 6f) * (PortRadius + 6f);
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private SKPoint ScreenToWorld(SKPoint screen)
    {
        return new SKPoint(screen.X / _zoom - _cameraOffset.X, screen.Y / _zoom - _cameraOffset.Y);
    }

    private sealed record NodeLayout(
        SKRect Bounds,
        Dictionary<string, SKPoint> InputPorts,
        Dictionary<string, SKPoint> OutputPorts);

    private sealed record NodeDragState(Guid NodeId, SKPoint Offset);

    private sealed record PanDragState(SKPoint StartScreen, SKPoint StartOffset);

    private sealed record ConnectionDragState(NodePortReference StartPort, SKPoint CurrentWorld);
}

public sealed class NodeDefinition
{
    public NodeDefinition(string name, IReadOnlyList<string> inputs, IReadOnlyList<string> outputs)
    {
        Name = name;
        Inputs = inputs;
        Outputs = outputs;
    }

    public string Name { get; }
    public IReadOnlyList<string> Inputs { get; }
    public IReadOnlyList<string> Outputs { get; }
}

public sealed class NodeInstance
{
    public NodeInstance(NodeDefinition definition, SKPoint position)
    {
        Id = Guid.NewGuid();
        Title = definition.Name;
        Inputs = definition.Inputs;
        Outputs = definition.Outputs;
        Position = position;
    }

    public Guid Id { get; }
    public string Title { get; }
    public IReadOnlyList<string> Inputs { get; }
    public IReadOnlyList<string> Outputs { get; }
    public SKPoint Position { get; set; }
}

public sealed class NodeConnection
{
    public NodeConnection(Guid fromNodeId, string fromPort, Guid toNodeId, string toPort)
    {
        FromNodeId = fromNodeId;
        FromPort = fromPort;
        ToNodeId = toNodeId;
        ToPort = toPort;
    }

    public Guid FromNodeId { get; }
    public string FromPort { get; }
    public Guid ToNodeId { get; }
    public string ToPort { get; }
}

public readonly record struct NodePortReference(Guid NodeId, string PortName, bool IsInput);

