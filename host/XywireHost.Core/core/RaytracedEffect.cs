namespace XywireHost.Core.core;

/// <summary>
///     Base class for effects that use raytracing to render pixels.
///     Handles ray generation and antialiasing.
/// </summary>
internal abstract class RaytracedEffect(LedLine attachedLedLine, int samplesPerPixel = 16)
    : AbstractEffect(attachedLedLine)
{
    private readonly Color[,] _colorsBuffer = new Color[attachedLedLine.Height, attachedLedLine.Width];
    private readonly (double x, double y)[] _samplingKernel = GenerateSamplingKernel(samplesPerPixel);

    private static (double x, double y)[] GenerateSamplingKernel(int sampleCount)
    {
        (double x, double y)[] kernel = new (double x, double y)[sampleCount];

        // Use rotated grid sampling for better quality than regular grid
        // This avoids aliasing patterns while maintaining consistent samples per frame
        int gridSize = (int)Math.Sqrt(sampleCount);
        const double angle = 26.565; // Magic angle for rotated grid (arctan(0.5))
        const double angleRad = angle * Math.PI / 180.0;
        double cos = Math.Cos(angleRad);
        double sin = Math.Sin(angleRad);

        int index = 0;
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                if (index >= sampleCount) break;

                // Position in regular grid [-0.5, 0.5]
                double x = (i + 0.5) / gridSize - 0.5;
                double y = (j + 0.5) / gridSize - 0.5;

                // Apply rotation
                double rotX = x * cos - y * sin;
                double rotY = x * sin + y * cos;

                // Wrap to [-0.5, 0.5] range
                while (rotX > 0.5) rotX -= 1.0;
                while (rotX < -0.5) rotX += 1.0;
                while (rotY > 0.5) rotY -= 1.0;
                while (rotY < -0.5) rotY += 1.0;

                kernel[index++] = (rotX, rotY);
            }
        }

        return kernel;
    }

    protected override void MoveNext()
    {
        int width = LedLine.Width;
        int height = LedLine.Height;
        int samplesPerPixel = _samplingKernel.Length;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Accumulate colors from multiple samples for antialiasing
                int totalR = 0, totalG = 0, totalB = 0;

                for (int sample = 0; sample < samplesPerPixel; sample++)
                {
                    // Use pre-computed sampling kernel for consistent, flicker-free antialiasing
                    (double offsetX, double offsetY) = _samplingKernel[sample];

                    // Map LED position to normalized screen coordinates [-1, 1]
                    double screenX = ((x + offsetX) / width - 0.5) * 2.0;
                    double screenY = ((y + offsetY) / height - 0.5) * 2.0;

                    // Render pixel - implemented by derived class
                    Color hitColor = RenderPixel(screenX, screenY);

                    totalR += hitColor.Red;
                    totalG += hitColor.Green;
                    totalB += hitColor.Blue;
                }

                // Average the samples
                _colorsBuffer[y, x] = Color.RGB(
                    totalR / samplesPerPixel,
                    totalG / samplesPerPixel,
                    totalB / samplesPerPixel
                );
            }
        }

        LedLine.SetColors(_colorsBuffer);
    }

    /// <summary>
    ///     Render a single pixel at the given normalized screen coordinates.
    /// </summary>
    /// <param name="x">Normalized X coordinate in range [-1, 1]</param>
    /// <param name="y">Normalized Y coordinate in range [-1, 1]</param>
    /// <returns>Color for this pixel</returns>
    protected abstract Color RenderPixel(double x, double y);
}
