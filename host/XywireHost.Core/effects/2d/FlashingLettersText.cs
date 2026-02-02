using System.Drawing;
using XywireHost.Core.core;
using Color = XywireHost.Core.core.Color;

namespace XywireHost.Core.effects._2d;

internal class FlashingLettersText : AbstractEffect
{
    private const string CharactersString =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

    private const string Text = "Happy New Year!";

    private readonly Color[][] _colorsBuffer;
    private readonly Color[][][] _characters;
    private readonly Dictionary<char, int> _charToIndex = new();
    private readonly int _totalTextWidth = Text.Length * 12;

    private int _frameNumber;
    private double _gradientOffset;
    private int _scrollOffset;

    public FlashingLettersText(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _colorsBuffer = Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);
        _characters = LoadCharacters();

        for (int i = 0; i < CharactersString.Length; i++)
        {
            _charToIndex[CharactersString[i]] = i;
        }
    }

    protected override void MoveNext()
    {
        for (int row = 0; row < LedLine.Height; row++)
        {
            for (int col = 0; col < LedLine.Width; col++)
            {
                _colorsBuffer[row][col] = Color.RGB(0, 0, 0);
            }
        }

        for (int charIdx = 0; charIdx < Text.Length; charIdx++)
        {
            char currentChar = Text[charIdx];
            int charIndex = _charToIndex.GetValueOrDefault(currentChar, 0);
            Color[][] charBitmap = _characters[charIndex];

            int charStartX = charIdx * 12 - _scrollOffset;

            for (int row = 0; row < 14 && row < LedLine.Height; row++)
            {
                for (int col = 1; col < 13; col++)
                {
                    int screenX = charStartX + col;
                    if (screenX < 0 || screenX >= LedLine.Width) continue;

                    double gradientValue = (screenX + row + _gradientOffset) / (LedLine.Width + LedLine.Height);
                    gradientValue = (gradientValue % 1.0 + 1.0) % 1.0;

                    Color purple = Color.RGB(150, 0, 255);
                    Color blue = Color.RGB(0, 150, 255);
                    Color gradientColor = Color.Lerp(purple, blue, gradientValue);

                    _colorsBuffer[row][screenX] = charBitmap[row][col] * gradientColor;
                }
            }
        }

        if (_frameNumber++ % 4 == 0)
        {
            _scrollOffset++;
            if (_scrollOffset >= _totalTextWidth + LedLine.Width)
            {
                _scrollOffset = 0;
            }
        }

        _gradientOffset += 0.5;
        if (_gradientOffset > LedLine.Width + LedLine.Height)
        {
            _gradientOffset = 0;
        }

        _colorsBuffer.MirrorLeftToRight();

        LedLine.SetColors(_colorsBuffer);
    }

    private static Color[][][] LoadCharacters()
    {
        Bitmap bitmapFile = new("./font2bitmap.png");
        Color[][][] result = new Color[CharactersString.Length][][];

        for (int charIndex = 0; charIndex < CharactersString.Length; charIndex++)
        {
            result[charIndex] = new Color[14][];
            for (int row = 0; row < 14; row++)
            {
                result[charIndex][row] = new Color[14];
                for (int col = 0; col < 14; col++)
                {
                    System.Drawing.Color pixel = bitmapFile.GetPixel(charIndex * 14 + col, row);
                    result[charIndex][row][col] = Color.RGB(pixel.R / 255.0 * pixel.A, pixel.G / 255.0 * pixel.A,
                        pixel.B / 255.0 * pixel.A);
                }
            }
        }

        return result;
    }

    protected override int StabilizeFps() => 60;
}
