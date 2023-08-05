using Media.Common;

public class BitmapInfoHeader : MemorySegment
{
    public const int Length = 40;

    public int Size
    {
        get => Binary.Read32(Array, Offset, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset, Binary.IsBigEndian, value);
    }

    public int Width
    {
        get => Binary.Read32(Array, Offset + 4, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset + 4, Binary.IsBigEndian, value);
    }

    public int Height
    {
        get => Binary.Read32(Array, Offset + 8, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset + 8, Binary.IsBigEndian, value);
    }

    public short Planes
    {
        get => Binary.Read16(Array, Offset + 12, Binary.IsBigEndian);
        set => Binary.Write16(Array, Offset + 12, Binary.IsBigEndian, value);
    }

    public short BitCount
    {
        get => Binary.Read16(Array, Offset + 14, Binary.IsBigEndian);
        set => Binary.Write16(Array, Offset + 14, Binary.IsBigEndian, value);
    }

    public int Compression
    {
        get => Binary.Read32(Array, Offset + 16, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset + 16, Binary.IsBigEndian, value);
    }

    public int ImageSize
    {
        get => Binary.Read32(Array, Offset + 20, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset + 20, Binary.IsBigEndian, value);
    }

    public int XPelsPerMeter
    {
        get => Binary.Read32(Array, Offset + 24, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset + 24, Binary.IsBigEndian, value);
    }

    public int YPelsPerMeter
    {
        get => Binary.Read32(Array, Offset + 28, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset + 28, Binary.IsBigEndian, value);
    }

    public int ColorsUsed
    {
        get => Binary.Read32(Array, Offset + 32, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset + 32, Binary.IsBigEndian, value);
    }

    public int ColorsImportant
    {
        get => Binary.Read32(Array, Offset + 36, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset + 36, Binary.IsBigEndian, value);
    }

    public BitmapInfoHeader(byte[] array, int offset)
        : base(array, offset, Length)
    {
    }

    public BitmapInfoHeader(int size, int width, int height, short planes, short bitCount, int compression, int imageSize, int xPelsPerMeter, int yPelsPerMeter, int colorsUsed, int colorsImportant)
        : base(new byte[Length])
    {
        Size = size;
        Width = width;
        Height = height;
        Planes = planes;
        BitCount = bitCount;
        Compression = compression;
        ImageSize = imageSize;
        XPelsPerMeter = xPelsPerMeter;
        YPelsPerMeter = yPelsPerMeter;
        ColorsUsed = colorsUsed;
        ColorsImportant = colorsImportant;
    }
}
