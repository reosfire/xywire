namespace XywireHost.Core.core;

internal static class Array2D
{
    public static void ShiftDown<T>(this T[][] array)
    {
        for (int i = array.Length - 1; i >= 1; i--)
        {
            Array.Copy(array[i - 1], array[i], array[i].Length);
        }
    }

    public static void ShiftDown<T>(this T[][] array, int mask)
    {
        for (int i = array.Length - 1; i >= 1; i--)
        {
            for (int j = 0; j < array[i].Length; j++)
            {
                if ((mask & (1 << j)) != 0)
                {
                    array[i][j] = array[i - 1][j];
                }
            }
        }
    }

    public static void ShiftUp<T>(this T[][] array, int mask)
    {
        for (int i = 0; i < array.Length - 1; i++)
        {
            for (int j = 0; j < array[i].Length; j++)
            {
                if ((mask & (1 << j)) != 0)
                {
                    array[i][j] = array[i + 1][j];
                }
            }
        }
    }

    public static void Shift<T>(this T[] array)
    {
        for (int i = array.Length - 1; i >= 1; i--)
        {
            array[i] = array[i - 1];
        }
    }

    public static void MirrorLeftToRight<T>(this T[][] array)
    {
        int width = array[0].Length;
        int height = array.Length;

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width / 2; col++)
            {
                (array[row][col], array[row][width - col - 1]) = (array[row][width - col - 1], array[row][col]);
            }
        }
    }

    public static void Fill<T>(this T[][] array, T value)
    {
        foreach (T[] row in array)
        {
            Array.Fill(row, value);
        }
    }

    public static unsafe void Fill<T>(this T[,] array, T value) where T : unmanaged
    {
        fixed (T* a = &array[0, 0])
        {
            Span<T> span = new(a, array.Length);
            span.Fill(value);
        }
    }

    public static T[][] CreateJagged<T>(int rows, int cols, T defaultValue)
    {
        T[][] array = new T[rows][];
        for (int i = 0; i < rows; i++)
        {
            array[i] = new T[cols];
            for (int j = 0; j < cols; j++)
            {
                array[i][j] = defaultValue;
            }
        }

        return array;
    }

    public static T[][] CreateJagged<T>(int rows, int cols)
    {
        T[][] array = new T[rows][];
        for (int i = 0; i < rows; i++)
        {
            array[i] = new T[cols];
        }

        return array;
    }
}
