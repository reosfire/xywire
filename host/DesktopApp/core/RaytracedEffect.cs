namespace Leds.core
{
    /// <summary>
    /// Base class for effects that use raytracing to render pixels.
    /// Handles ray generation, antialiasing with sampling kernel, and frame rendering.
    /// </summary>
    internal abstract class RaytracedEffect(LedLine attachedLedLine, int samplesPerPixel = 16)
        : AbstractEffect(attachedLedLine)
    {
        private readonly Color[,] _colorsBuffer = new Color[attachedLedLine.Height, attachedLedLine.Width];
        private readonly (double x, double y)[] _samplingKernel = GenerateSamplingKernel(samplesPerPixel);

        private static (double x, double y)[] GenerateSamplingKernel(int sampleCount)
        {
            var kernel = new (double x, double y)[sampleCount];
            
            // Use rotated grid sampling for better quality than regular grid
            // This avoids aliasing patterns while maintaining consistent samples per frame
            var gridSize = (int)Math.Sqrt(sampleCount);
            const double angle = 26.565; // Magic angle for rotated grid (arctan(0.5))
            const double angleRad = angle * Math.PI / 180.0;
            var cos = Math.Cos(angleRad);
            var sin = Math.Sin(angleRad);
            
            var index = 0;
            for (var i = 0; i < gridSize; i++)
            {
                for (var j = 0; j < gridSize; j++)
                {
                    if (index >= sampleCount) break;
                    
                    // Position in regular grid [-0.5, 0.5]
                    var x = (i + 0.5) / gridSize - 0.5;
                    var y = (j + 0.5) / gridSize - 0.5;
                    
                    // Apply rotation
                    var rotX = x * cos - y * sin;
                    var rotY = x * sin + y * cos;
                    
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
            var width = LedLine.Width;
            var height = LedLine.Height;
            var samplesPerPixel = _samplingKernel.Length;

            // Raytracing for each pixel
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    // Accumulate colors from multiple samples for antialiasing
                    int totalR = 0, totalG = 0, totalB = 0;

                    for (var sample = 0; sample < samplesPerPixel; sample++)
                    {
                        // Use pre-computed sampling kernel for consistent, flicker-free antialiasing
                        var (offsetX, offsetY) = _samplingKernel[sample];

                        // Map LED position to normalized screen coordinates [-1, 1]
                        var screenX = ((x + offsetX) / width - 0.5) * 2.0;
                        var screenY = ((y + offsetY) / height - 0.5) * 2.0;

                        // Render pixel - implemented by derived class
                        var hitColor = RenderPixel(screenX, screenY);

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
        /// Render a single pixel at the given normalized screen coordinates.
        /// </summary>
        /// <param name="x">Normalized X coordinate in range [-1, 1]</param>
        /// <param name="y">Normalized Y coordinate in range [-1, 1]</param>
        /// <returns>Color for this pixel</returns>
        protected abstract Color RenderPixel(double x, double y);
    }
}

