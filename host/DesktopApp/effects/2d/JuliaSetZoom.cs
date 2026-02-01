using Leds.core;

namespace Leds.effects._2d;

/// <summary>
///     Infinitely zooming Julia set fractal that intelligently finds and zooms towards edge points
/// </summary>
internal class JuliaSetZoom(LedLine attachedLedLine) : RaytracedEffect(attachedLedLine, 4)
{
    private double _cImag = 0.27015, _cReal = -0.7;
    private int _framesSinceLastSearch = 0;
    private double _hueOffset = 0.0;
    private double _offsetX = 0.0, _offsetY = 0.0;
    private double _targetX = 0.0, _targetY = 0.0;
    private double _time = 0.0;
    private double _zoom = 1.0;

    protected override void MoveNext()
    {
        // Search for interesting edge points periodically
        _framesSinceLastSearch++;
        if (_framesSinceLastSearch > 20) // Search every 20 frames
        {
            FindEdgePoint();
            _framesSinceLastSearch = 0;
        }

        // Animate the zoom - exponential zoom in, then reset
        _zoom *= 1.015; // Zoom in 1.5% per frame

        if (_zoom > 500.0) // Reset after zooming in too far
        {
            _zoom = 1.0;
            _offsetX = 0.0;
            _offsetY = 0.0;
            _targetX = 0.0;
            _targetY = 0.0;
        }

        // Move towards the target edge point
        double moveSpeed = 0.02 / _zoom;
        _offsetX += (_targetX - _offsetX) * moveSpeed;
        _offsetY += (_targetY - _offsetY) * moveSpeed;

        // Animate the Julia set parameters for morphing effect (slower)
        _time += 0.02;
        _cReal = -0.7 + Math.Sin(_time * 0.1) * 0.05;
        _cImag = 0.27015 + Math.Cos(_time * 0.15) * 0.025;

        // Rotate hue for rainbow effect
        _hueOffset += 2.0;
        if (_hueOffset >= 360.0) _hueOffset -= 360.0;

        // Call base class to render the frame
        base.MoveNext();
    }

    private void FindEdgePoint()
    {
        // Sample points in a spiral pattern to find interesting edge points
        const int samples = 20;
        double bestScore = 0;
        double bestX = _targetX;
        double bestY = _targetY;

        double searchRadius = 1.0 / _zoom; // Search radius shrinks as we zoom in

        for (int i = 0; i < samples; i++)
        {
            double angle = i * 2.4; // Golden angle for good distribution
            double radius = searchRadius * Math.Sqrt(i / (double)samples);

            double testX = _offsetX + Math.Cos(angle) * radius;
            double testY = _offsetY + Math.Sin(angle) * radius;

            // Test this point
            int iterations = TestPoint(testX, testY);

            // Score: prefer points on the edge (high iteration but not maxed out)
            // Points with 60-90% of max iterations are usually on interesting edges
            double score = 0;
            if (iterations > 0 && iterations < 128)
            {
                // Prefer points closer to max iterations (near the boundary)
                double normalizedIter = iterations / 128.0;
                if (normalizedIter > 0.6 && normalizedIter < 0.95)
                {
                    score = normalizedIter;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestX = testX;
                bestY = testY;
            }
        }

        // Update target if we found something good
        if (bestScore > 0)
        {
            _targetX = bestX;
            _targetY = bestY;
        }
    }

    private int TestPoint(double zReal, double zImag)
    {
        // Quick iteration test for a point
        int maxIterations = 128;
        int iteration = 0;

        double zr = zReal;
        double zi = zImag;

        while (iteration < maxIterations)
        {
            double zr2 = zr * zr;
            double zi2 = zi * zi;

            if (zr2 + zi2 > 4.0)
                break;

            double temp = zr2 - zi2 + _cReal;
            zi = 2.0 * zr * zi + _cImag;
            zr = temp;

            iteration++;
        }

        return iteration;
    }

    protected override Color RenderPixel(double x, double y)
    {
        // Apply zoom and offset to map screen coordinates to complex plane
        double zoomFactor = 2.0 / _zoom;
        double zReal = x * zoomFactor + _offsetX;
        double zImag = y * zoomFactor + _offsetY;

        // Julia set iteration
        const int maxIterations = 128;
        int iteration = 0;

        double zr = zReal;
        double zi = zImag;

        // Iterate z = z^2 + c
        while (iteration < maxIterations)
        {
            double zr2 = zr * zr;
            double zi2 = zi * zi;

            // Check if point escaped (magnitude > 2)
            if (zr2 + zi2 > 4.0)
                break;

            // z = z^2 + c
            double temp = zr2 - zi2 + _cReal;
            zi = 2.0 * zr * zi + _cImag;
            zr = temp;

            iteration++;
        }

        // Color based on iteration count
        if (iteration == maxIterations)
        {
            // Point is in the set - render as black
            return Color.RGB(0, 0, 0);
        }

        // Smooth coloring algorithm for better gradients
        double smoothed = iteration + 1 - Math.Log(Math.Log(zr * zr + zi * zi)) / Math.Log(2);

        // Map to hue with animated offset
        double hue = (smoothed * 10.0 + _hueOffset) % 360.0;

        // Vary saturation and value for depth
        double saturation = 0.8 + Math.Sin(smoothed * 0.5) * 0.2;
        double value = 0.5 + Math.Sin(smoothed * 0.3) * 0.5;

        return Color.HSV(hue, saturation, value);
    }

    protected override int StabilizeFps() => 30; // 30 FPS for smooth animation
}
