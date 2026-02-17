using System.Drawing;
using XywireHost.Core.core;
using Color = XywireHost.Core.core.Color;

namespace XywireHost.Core.effects._2d;

public class BadApple : AbstractEffect
{
    private readonly Color[][][] _animationFrames;
    private int _currentFrameIndex = 0;

    public BadApple(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _animationFrames = LoadAnimationFrames();
    }
    
    private static Color[][][] LoadAnimationFrames()
    {
        string[] frameFiles = Directory.GetFiles("frames");
        Color[][][] frames = new Color[frameFiles.Length][][];
        
        foreach (string frameFile in frameFiles)
        {
            Bitmap bitmapFile = new(frameFile);
            
            Color[][] frame = Array2D.CreateJagged<Color>(bitmapFile.Height, bitmapFile.Width);
            for (int y = 0; y < bitmapFile.Height; y++)
            {
                for (int x = 0; x < bitmapFile.Width; x++)
                {
                    System.Drawing.Color pixelColor = bitmapFile.GetPixel(x, y);
                    frame[y][x] = Color.RGB(pixelColor.R, pixelColor.G, pixelColor.B);
                }
            }
            
            int parsedFrameIndex = int.Parse(frameFile.TrimStart("frames\\frame_").TrimEnd(".png"));
            
            frame.MirrorLeftToRight();
            frames[parsedFrameIndex] = frame;
        }

        return frames;
    }

    protected override void MoveNext()
    {
        LedLine.SetColors(_animationFrames[_currentFrameIndex]);
        _currentFrameIndex = (_currentFrameIndex + 1) % _animationFrames.Length;
    }

    protected override int StabilizeFps() => 30;
}
