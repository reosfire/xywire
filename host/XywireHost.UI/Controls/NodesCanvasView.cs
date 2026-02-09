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

    private readonly Dictionary<int, NodeLayout> _layouts = new();

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

    private IDragState? _currentDragState;

    private SKPoint _cameraOffset = new(0f, 0f);
    private float _zoom = 1f;

    public NodesCanvasView()
    {
        EnableTouchEvents = true;
        Touch += OnTouch;
    }

    private int _currentNodeId = 0;
    public Dictionary<int, INodeInstance> Nodes { get; } = [];

    public int? SelectedNodeId { get; private set; }

    public event EventHandler<NodeSelectionChangedEventArgs>? SelectionChanged;

    public NodeInstance<T> AddNode<T>(
        SKPoint position,
        string title,
        List<string> inputPortLabels,
        List<string> outputPortLabels,
        T payload)
    {
        NodeInstance<T> node = new(
            id: _currentNodeId++,
            position: position,
            title: title,
            inputs: inputPortLabels.ToDictionary(label => label, _ => (PortReference?)null),
            outputs: outputPortLabels.ToDictionary(label => label, _ => (PortReference?)null),
            payload: payload
        );

        Nodes[node.Id] = node;
        SetSelectedNode(node.Id);

        InvalidateSurface();
        return node;
    }

    public void RemoveNode(int nodeId)
    {
        INodeInstance node = Nodes[nodeId];
        
        foreach ((string _, PortReference? nullablePort) in node.Inputs)
        {
            if (nullablePort is not { } connectedPort) continue;
            
            INodeInstance connectedNode = Nodes[connectedPort.NodeId];
            connectedNode.Outputs[connectedPort.PortId] = null;
        }
        
        foreach ((string _, PortReference? nullablePort) in node.Outputs)
        {
            if (nullablePort is not { } connectedPort) continue;
            
            INodeInstance connectedNode = Nodes[connectedPort.NodeId];
            connectedNode.Inputs[connectedPort.PortId] = null;
        }
        
        Nodes.Remove(nodeId);

        ClearSelection();
        InvalidateSurface();
    }

    public void AddConnection(PortReference outputPort, PortReference inputPort)
    {
        if (!Nodes.TryGetValue(outputPort.NodeId, out INodeInstance? outputNode))
            throw new ArgumentException($"Invalid output node ID({outputPort.NodeId}) provided.");
        if (!Nodes.TryGetValue(inputPort.NodeId, out INodeInstance? inputNode))
            throw new ArgumentException($"Invalid input node ID({inputPort.NodeId}) provided.");
        
        if (!outputNode.Outputs.TryGetValue(outputPort.PortId, out PortReference? inputPortConnectedToOutput))
            throw new ArgumentException($"Invalid output port name({outputPort.PortId}) provided.");
        if (!inputNode.Inputs.TryGetValue(inputPort.PortId, out PortReference? outputPortConnectedToInput))
            throw new ArgumentException($"Invalid input port name({inputPort.PortId}) provided.");
        
        if (inputPortConnectedToOutput is not null) RemoveConnectionByOutput(outputPort);
        if (outputPortConnectedToInput is not null) RemoveConnectionByInput(inputPort);
        
        outputNode.Outputs[outputPort.PortId] = inputPort;
        inputNode.Inputs[inputPort.PortId] = outputPort;
        
        InvalidateSurface();
    }
    
    private void RemoveConnectionByOutput(PortReference outputPort)
    {
        if (!Nodes.TryGetValue(outputPort.NodeId, out INodeInstance? outputNode))
            throw new ArgumentException($"Invalid output node ID({outputPort.NodeId}) provided.");
        
        if (!outputNode.Outputs.TryGetValue(outputPort.PortId, out PortReference? inputPortConnectedToOutput))
            throw new ArgumentException($"Invalid output port name({outputPort.PortId}) provided.");
        
        if (inputPortConnectedToOutput is null)
            throw new InvalidOperationException("Output port is not connected");
        
        INodeInstance inputNode = Nodes[inputPortConnectedToOutput.Value.NodeId];

        outputNode.Outputs[outputPort.PortId] = null;
        inputNode.Inputs[inputPortConnectedToOutput.Value.PortId] = null;

        InvalidateSurface();
    }
    
    private void RemoveConnectionByInput(PortReference inputPort)
    {
        if (!Nodes.TryGetValue(inputPort.NodeId, out INodeInstance? inputNode))
            throw new ArgumentException($"Invalid input node ID({inputPort.NodeId}) provided.");
        
        if (!inputNode.Inputs.TryGetValue(inputPort.PortId, out PortReference? outputPortConnectedToInput))
            throw new ArgumentException($"Invalid input port name({inputPort.PortId}) provided.");
        
        if (outputPortConnectedToInput is null)
            throw new InvalidOperationException("Input port is not connected");
        
        INodeInstance outputNode = Nodes[outputPortConnectedToInput.Value.NodeId];

        outputNode.Outputs[outputPortConnectedToInput.Value.PortId] = null;
        inputNode.Inputs[inputPort.PortId] = null;

        InvalidateSurface();
    }

    private bool TryRemoveConnection(PortReference outputPort, PortReference inputPort)
    {
        if (!Nodes.TryGetValue(outputPort.NodeId, out INodeInstance? outputNode))
            throw new ArgumentException($"Invalid output node ID({outputPort.NodeId}) provided.");
        if (!Nodes.TryGetValue(inputPort.NodeId, out INodeInstance? inputNode))
            throw new ArgumentException($"Invalid input node ID({inputPort.NodeId}) provided.");
        
        if (!outputNode.Outputs.TryGetValue(outputPort.PortId, out PortReference? inputPortConnectedToOutput))
            throw new ArgumentException($"Invalid output port name({outputPort.PortId}) provided.");
        if (!inputNode.Inputs.TryGetValue(inputPort.PortId, out PortReference? outputPortConnectedToInput))
            throw new ArgumentException($"Invalid input port name({inputPort.PortId}) provided.");
        
        if (inputPortConnectedToOutput != inputPort || outputPortConnectedToInput != outputPort)
        {
            return false;
        }
        
        outputNode.Outputs[outputPort.PortId] = null;
        inputNode.Inputs[inputPort.PortId] = null;

        InvalidateSurface();
        return true;
    }

    private void SetSelectedNode(int? nodeId)
    {
        if (SelectedNodeId == nodeId)
            return;
        SelectedNodeId = nodeId;
        
        if (nodeId == null) return;

        INodeInstance? selectedNode = Nodes.GetValueOrDefault(nodeId.Value);
        SelectionChanged?.Invoke(this, new NodeSelectionChangedEventArgs(selectedNode));
        InvalidateSurface();
    }

    private void ClearSelection() => SetSelectedNode(null);

    public void FitToContent(float padding = 40f)
    {
        if (Nodes.Count == 0 || CanvasSize.Width <= 0f || CanvasSize.Height <= 0f)
        {
            return;
        }

        SKRect? bounds = null;
        foreach (INodeInstance node in Nodes.Values)
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

        foreach (INodeInstance node in Nodes.Values)
        {
            NodeLayout layout = BuildLayout(node);
            _layouts[node.Id] = layout;

            bool isSelected = SelectedNodeId == node.Id;
            SKPaint fillPaint = isSelected ? _selectedNodePaint : _nodePaint;

            canvas.DrawRoundRect(layout.Bounds, 8, 8, fillPaint);
            canvas.DrawRoundRect(layout.Bounds, 8, 8, _nodeStrokePaint);

            float titleBaseline = layout.Bounds.Top + NodePadding + HeaderHeight;
            canvas.DrawText(node.Title, layout.Bounds.Left + NodePadding, titleBaseline, _titlePaint);

            foreach ((string portName, SKPoint position) in layout.InputPorts)
            {
                DrawPort(canvas, position);
                canvas.DrawText(portName, position.X + 10f, position.Y + 5f, _textPaint);
            }

            foreach ((string portName, SKPoint position) in layout.OutputPorts)
            {
                DrawPort(canvas, position);
                float textWidth = _textPaint.MeasureText(portName);
                canvas.DrawText(portName, position.X - 10f - textWidth, position.Y + 5f, _textPaint);
            }
        }

        foreach ((int nodeId, INodeInstance node) in Nodes)
        {
            foreach ((string outputPortId, PortReference? connectedOutput) in node.Outputs)
            {
                if (connectedOutput is null) continue;
                
                if (!_layouts.TryGetValue(nodeId, out NodeLayout? fromLayout) ||
                    !_layouts.TryGetValue(connectedOutput.Value.NodeId, out NodeLayout? toLayout))
                {
                    continue;
                }

                if (!fromLayout.OutputPorts.TryGetValue(outputPortId, out SKPoint start) ||
                    !toLayout.InputPorts.TryGetValue(connectedOutput.Value.PortId, out SKPoint end))
                {
                    continue;
                }

                DrawConnection(canvas, start, end);
            }
        }

        if (_currentDragState is ConnectionDragState drag &&
            TryGetPortPosition(drag.StartPort, out SKPoint startPosition))
        {
            DrawConnection(canvas, startPosition, drag.CurrentWorld);
        }
    }

    private NodeLayout BuildLayout(INodeInstance node)
    {
        float titleWidth = _titlePaint.MeasureText(node.Title);
        float maxInputWidth = node.Inputs.Count == 0 ? 0f : node.Inputs.Max(input => _textPaint.MeasureText(input.Key));
        float maxOutputWidth =
            node.Outputs.Count == 0 ? 0f : node.Outputs.Max(output => _textPaint.MeasureText(output.Key));

        float leftColumnWidth = Math.Max(60f, maxInputWidth + 16f);
        float rightColumnWidth = Math.Max(60f, maxOutputWidth + 16f);
        float width = Math.Max(MinNodeWidth, NodePadding * 2 + leftColumnWidth + rightColumnWidth + 20f);
        width = Math.Max(width, NodePadding * 2 + titleWidth + 10f);

        int rowCount = Math.Max(node.Inputs.Count, node.Outputs.Count);
        float height = NodePadding * 2 + HeaderHeight + rowCount * LineHeight;

        SKRect bounds = new(node.Position.X, node.Position.Y, node.Position.X + width, node.Position.Y + height);
        Dictionary<string, SKPoint> inputPorts = new();
        Dictionary<string, SKPoint> outputPorts = new();

        float y = bounds.Top + NodePadding + HeaderHeight + LineHeight / 2f;
        foreach (string port in node.Inputs.Keys)
        {
            inputPorts[port] = new SKPoint(bounds.Left + NodePadding, y);
            y += LineHeight;
        }
        
        y = bounds.Top + NodePadding + HeaderHeight + LineHeight / 2f;
        foreach (string port in node.Outputs.Keys)
        {
            outputPorts[port] = new SKPoint(bounds.Right - NodePadding, y);
            y += LineHeight;
        }

        return new NodeLayout(bounds, inputPorts, outputPorts);
    }

    private void DrawPort(SKCanvas canvas, SKPoint position) => canvas.DrawCircle(position, PortRadius, _portPaint);

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
        bool eventHandled = true;

        switch (e.ActionType)
        {
            case SKTouchAction.WheelChanged:
                {
                    float zoomFactor = (float)Math.Pow(1.1f, e.WheelDelta / 120f);
                    float newZoom = Clamp(_zoom * zoomFactor, MinZoom, MaxZoom);
                    if (Math.Abs(newZoom - _zoom) > 0.0001f)
                    {
                        SKPoint worldBefore = ScreenToWorld(e.Location);
                        _zoom = newZoom;
                        _cameraOffset = e.Location / _zoom - worldBefore;
                    }

                    break;
                }
            case SKTouchAction.Pressed:
                {
                    SKPoint worldLocation = ScreenToWorld(e.Location);
                    if (TryHitPort(worldLocation, out PortReference port))
                    {
                        SetSelectedNode(port.NodeId);
                        _currentDragState = new ConnectionDragState(port, worldLocation);
                    }
                    else if (TryHitNode(worldLocation, out int nodeId, out SKPoint nodeOrigin))
                    {
                        SetSelectedNode(nodeId);
                        _currentDragState = new NodeDragState(nodeId, worldLocation - nodeOrigin);
                    }
                    else
                    {
                        ClearSelection();
                        _currentDragState = new PanDragState(e.Location, _cameraOffset);
                    }

                    break;
                }
            case SKTouchAction.Moved when _currentDragState is ConnectionDragState connectionDrag:
                {
                    _currentDragState = connectionDrag with { CurrentWorld = ScreenToWorld(e.Location) };
                    break;
                }
            case SKTouchAction.Moved when _currentDragState is NodeDragState nodeDrag:
                {
                    INodeInstance node = Nodes[nodeDrag.NodeId];
                    node.Position = ScreenToWorld(e.Location) - nodeDrag.Offset;

                    break;
                }
            case SKTouchAction.Moved when _currentDragState is PanDragState panDrag:
                {
                    SKPoint deltaScreen = e.Location - panDrag.StartScreen;
                    _cameraOffset = panDrag.StartOffset + deltaScreen / _zoom;

                    break;
                }
            case SKTouchAction.Released or SKTouchAction.Cancelled when
                _currentDragState is ConnectionDragState releasedConnectionDrag:
                {
                    SKPoint worldLocation = ScreenToWorld(e.Location);
                    if (TryHitPort(worldLocation, out PortReference targetPort))
                    {
                        ToggleConnection(releasedConnectionDrag.StartPort, targetPort);
                    }

                    _currentDragState = null;
                    break;
                }
            case SKTouchAction.Released or SKTouchAction.Cancelled:
                {
                    _currentDragState = null;
                    break;
                }
            default:
                {
                    eventHandled = false;
                    break;
                }
        }

        if (!eventHandled) return;

        e.Handled = true;
        InvalidateSurface();
    }

    private bool TryHitNode(SKPoint location, out int nodeId, out SKPoint nodeOrigin)
    {
        foreach ((int id, NodeLayout layout) in _layouts)
        {
            if (layout.Bounds.Contains(location))
            {
                nodeId = id;
                nodeOrigin = new SKPoint(layout.Bounds.Left, layout.Bounds.Top);
                return true;
            }
        }

        nodeId = -1;
        nodeOrigin = default;
        return false;
    }

    private bool TryHitPort(SKPoint location, out PortReference port)
    {
        foreach ((int id, NodeLayout layout) in _layouts)
        {
            foreach ((string portName, SKPoint position) in layout.InputPorts)
            {
                if (!IsNear(location, position)) continue;
                
                port = new PortReference(id, portName, PortType.Input);
                return true;
            }

            foreach ((string portName, SKPoint position) in layout.OutputPorts)
            {
                if (!IsNear(location, position)) continue;
                
                port = new PortReference(id, portName, PortType.Output);
                return true;
            }
        }

        port = default;
        return false;
    }

    private void ToggleConnection(PortReference a, PortReference b)
    {
        if (a.NodeId == b.NodeId) return;
        
        if (a.Type == PortType.Input && b.Type == PortType.Input) return;
        if (a.Type == PortType.Output && b.Type == PortType.Output) return;

        PortReference output = a.Type == PortType.Output ? a : b;
        PortReference input = a.Type == PortType.Input ? a : b;

        if (!TryRemoveConnection(output, input)) AddConnection(output, input);
    }

    private bool TryGetPortPosition(PortReference port, out SKPoint position)
    {
        if (_layouts.TryGetValue(port.NodeId, out NodeLayout? layout))
        {
            Dictionary<string, SKPoint> ports = port.Type == PortType.Input ? layout.InputPorts : layout.OutputPorts;
            if (ports.TryGetValue(port.PortId, out position))
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

    private SKPoint ScreenToWorld(SKPoint screen) =>
        new(screen.X / _zoom - _cameraOffset.X, screen.Y / _zoom - _cameraOffset.Y);

    private sealed record NodeLayout(
        SKRect Bounds,
        Dictionary<string, SKPoint> InputPorts,
        Dictionary<string, SKPoint> OutputPorts
    );

    private interface IDragState;

    private sealed record NodeDragState(int NodeId, SKPoint Offset) : IDragState;

    private sealed record PanDragState(SKPoint StartScreen, SKPoint StartOffset) : IDragState;

    private sealed record ConnectionDragState(PortReference StartPort, SKPoint CurrentWorld) : IDragState;
}

public enum PortType
{
    Input,
    Output,
}

public readonly record struct PortReference(int NodeId, string PortId, PortType Type);

public interface INodeInstance
{
    int Id { get; }
    SKPoint Position { get; set; }

    string Title { get; }
    Dictionary<string, PortReference?> Inputs { get; }
    Dictionary<string, PortReference?> Outputs { get; }
}

public class NodeInstance<T>(
    int id,
    SKPoint position,
    string title,
    Dictionary<string, PortReference?> inputs,
    Dictionary<string, PortReference?> outputs,
    T payload
) : INodeInstance
{
    public int Id { get; } = id;
    public SKPoint Position { get; set; } = position;

    public string Title { get; } = title;
    public Dictionary<string, PortReference?> Inputs { get; } = inputs;
    public Dictionary<string, PortReference?> Outputs { get; } = outputs;

    public T Payload { get; set; } = payload;
}

public sealed class NodeSelectionChangedEventArgs(INodeInstance? selectedNode) : EventArgs
{
    public INodeInstance? SelectedNode { get; } = selectedNode;
}

// TODO move this to utils
public static class Operators
{
    extension(SKPoint)
    {
        public static SKPoint operator /(SKPoint point, float factor) =>
            new(point.X / factor, point.Y / factor);
    }
}
