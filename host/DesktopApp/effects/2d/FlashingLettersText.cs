using System.Drawing;
using Leds.core;
using Color = Leds.core.Color;

namespace Leds.effects._2d;

internal class FlashingLettersText : AbstractEffect
{
    private readonly Color[][] _colorsBuffer;
    private Color[][][] _characters;
    private string _charactersString =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
    private Dictionary<char, int> _charToIndex = new();
    
    private string _text = "Happy New Year!";
    private int _scrollOffset = 0;
    private int _totalTextWidth;
    private double _gradientOffset = 0;

    public FlashingLettersText(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _colorsBuffer = Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);

        LoadCharacters();

        for (int i = 0; i < _charactersString.Length; i++)
        {
            _charToIndex[_charactersString[i]] = i;
        }

        _totalTextWidth = _text.Length * 12;
    }
    
    private int _frameCounter = 0;

    protected override void MoveNext()
    {
        for (int row = 0; row < LedLine.Height; row++)
        {
            for (int col = 0; col < LedLine.Width; col++)
            {
                _colorsBuffer[row][col] = Color.RGB(0, 0, 0);
            }
        }

        for (int charIdx = 0; charIdx < _text.Length; charIdx++)
        {
            var currentChar = _text[charIdx];
            var charIndex = _charToIndex.ContainsKey(currentChar) ? _charToIndex[currentChar] : 0;
            var charBitmap = _characters[charIndex];
            
            int charStartX = charIdx * 12 - _scrollOffset;
            
            for (int row = 0; row < 14 && row < LedLine.Height; row++)
            {
                for (int col = 1; col < 13; col++)
                {
                    int screenX = charStartX + col;
                    if (screenX >= 0 && screenX < LedLine.Width)
                    {
                        double gradientValue = (screenX + row + _gradientOffset) / (LedLine.Width + LedLine.Height);
                        gradientValue = (gradientValue % 1.0 + 1.0) % 1.0;
                        
                        Color purple = Color.RGB(150, 0, 255);
                        Color blue = Color.RGB(0, 150, 255);
                        Color gradientColor = Color.Lerp(purple, blue, gradientValue);
                        
                        _colorsBuffer[row][screenX] = charBitmap[row][col] * gradientColor;
                    }
                }
            }
        }

        if (_frameCounter++ % 4 == 0)
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
    
    private void LoadCharacters()
    {
        var bitmapFile = new Bitmap("./font2bitmap.png");
        _characters = new Color[_charactersString.Length][][];
        
        for (int charIndex = 0; charIndex < _charactersString.Length; charIndex++)
        {
            _characters[charIndex] = new Color[14][];
            for (int row = 0; row < 14; row++)
            {
                _characters[charIndex][row] = new Color[14];
                for (int col = 0; col < 14; col++)
                {
                    var pixel = bitmapFile.GetPixel(charIndex * 14 + col, row);
                    _characters[charIndex][row][col] = Color.RGB(pixel.R / 255.0 * pixel.A, pixel.G / 255.0 * pixel.A, pixel.B / 255.0 * pixel.A);
                }
            }
        }
    }

    protected override int StabilizeFps()
    {
        return 60;
    }
}