using System.Diagnostics;
using XywireHost.Core.core;

namespace ExamplePlugin;

public class LoadedEffect : LoadedEffectBase
{
    private int _currentFrame = 0;

    public override void FillFrame(Color[][] buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            for (int j = 0; j < buffer.Length; j++)
            {
                int red = (_currentFrame + i * 10) % 256;
                int green = (_currentFrame + j * 10) % 256;
                int blue = _currentFrame % 256;
                buffer[i][j] = Color.RGB(red, green, blue);
            }
        }

        _currentFrame++;
    }
}
