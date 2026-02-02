namespace XywireHost.Core.core;

public readonly struct Color
{
    public readonly byte Red, Green, Blue;

    private byte Max => Math.Max(Red, Math.Max(Green, Blue));
    private byte Min => Math.Min(Red, Math.Min(Green, Blue));

    public double Hue
    {
        get
        {
            if (Red == Green && Green == Blue)
                return 0;

            double delta = Max - Min;
            double hue;

            if (Red == Max)
                hue = (Green - Blue) / delta;
            else if (Green == Max)
                hue = (Blue - Red) / delta + 2f;
            else
                hue = (Red - Green) / delta + 4f;

            hue *= 60;
            if (hue < 0)
                hue += 360;

            return hue;
        }
    }

    public double Saturation => Max == 0 ? 0 : 1d - 1d * Min / Max;
    public double Value => Max / 255d;

    public Color(byte r, byte g, byte b)
    {
        Red = r;
        Green = g;
        Blue = b;
    }

    public Color CopyRGB(int? red = null, int? green = null, int? blue = null) =>
        RGB(red ?? Red, green ?? Green, blue ?? Blue);

    public Color CopyHSV(double? hue = null, double? saturation = null, double? value = null) =>
        HSV(hue ?? Hue, saturation ?? Saturation, value ?? Value);

    public override string ToString() => $"{Red} {Green} {Blue}";

    public static Color RGB(double red, double green, double blue)
    {
        EnsureValidRGBComponent(red, nameof(red));
        EnsureValidRGBComponent(green, nameof(green));
        EnsureValidRGBComponent(blue, nameof(blue));

        return new Color((byte)red, (byte)green, (byte)blue);
    }

    public static Color RGB(int red, int green, int blue)
    {
        EnsureValidRGBComponent(red, nameof(red));
        EnsureValidRGBComponent(green, nameof(green));
        EnsureValidRGBComponent(blue, nameof(blue));

        return new Color((byte)red, (byte)green, (byte)blue);
    }

    public static Color RGB(byte r, byte g, byte b) => new(r, g, b);

    //public static Color HSV(double hue, double saturation, double value)
    //{
    //    EnsureValidHUE(hue);
    //    EnsureValidFraction(saturation, nameof(saturation));
    //    EnsureValidFraction(value, nameof(value));

    //    int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
    //    double f = hue / 60 - Math.Floor(hue / 60);

    //    value *= 255;
    //    int v = Convert.ToInt32(value);
    //    int p = Convert.ToInt32(value * (1 - saturation));
    //    int q = Convert.ToInt32(value * (1 - f * saturation));
    //    int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

    //    return hi switch
    //    {
    //        0 => RGB(v, t, p),
    //        1 => RGB(q, v, p),
    //        2 => RGB(p, v, t),
    //        3 => RGB(p, q, v),
    //        4 => RGB(t, p, v),
    //        _ => RGB(v, p, q)
    //    };
    //}

    public static Color HSV(double hue, double saturation, double value)
    {
        EnsureValidHUE(hue);
        EnsureValidFraction(saturation, nameof(saturation));
        EnsureValidFraction(value, nameof(value));

        double chroma = value * saturation;
        double x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
        double m = value - chroma;

        double rPrime = 0, gPrime = 0, bPrime = 0;

        switch (hue)
        {
            case >= 0 and < 60:
                rPrime = chroma;
                gPrime = x;
                break;
            case >= 60 and < 120:
                rPrime = x;
                gPrime = chroma;
                break;
            case >= 120 and < 180:
                gPrime = chroma;
                bPrime = x;
                break;
            case >= 180 and < 240:
                gPrime = x;
                bPrime = chroma;
                break;
            case >= 240 and < 300:
                rPrime = x;
                bPrime = chroma;
                break;
            case >= 300 and <= 360:
                rPrime = chroma;
                bPrime = x;
                break;
        }

        byte r = (byte)((rPrime + m) * 255);
        byte g = (byte)((gPrime + m) * 255);
        byte b = (byte)((bPrime + m) * 255);

        return new Color(r, g, b);
    }

    public static Color Lerp(Color a, Color b, double fraction) => RGB(a.Red + (b.Red - a.Red) * fraction,
        a.Green + (b.Green - a.Green) * fraction, a.Blue + (b.Blue - a.Blue) * fraction);

    public static Color operator *(Color a, Color b)
    {
        return RGB(
            a.Red / 255.0 * b.Red,
            a.Green / 255.0 * b.Green,
            a.Blue / 255.0 * b.Blue
        );
    }

    public static Color operator *(Color a, double factor) => RGB(a.Red * factor, a.Green * factor, a.Blue * factor);

    private static void EnsureValidRGBComponent(int value, string name)
    {
        if (value < 0 || value > 255)
            throw new ArgumentOutOfRangeException(name, $"Was: {value}, but should be in [0, 255]");
    }

    private static void EnsureValidRGBComponent(double value, string name)
    {
        if (value < 0 || value > 255)
            throw new ArgumentOutOfRangeException(name, $"Was: {value}, but should be in [0, 255]");
    }

    private static void EnsureValidHUE(double hue)
    {
        if (hue < 0 || hue > 360)
            throw new ArgumentOutOfRangeException(nameof(hue), $"Was: {hue}, but should be in [0, 360]");
    }

    private static void EnsureValidFraction(double fraction, string name)
    {
        if (fraction < 0 || fraction > 1)
            throw new ArgumentOutOfRangeException(name, $"Was: {fraction}, but should be in [0, 1]");
    }
}
