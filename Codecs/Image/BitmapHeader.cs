using Media.Common;

namespace Codecs.Image;

public class BitmapHeader : MemorySegment
{
    public const int Length = 14;

    public const ushort BMFileSignature = 0x424D; // "BM" in ASCII

    public const ushort BAFileSignature = 0x4142; // "BA" in ASCII

    public const ushort CIFileSignature = 0x4943; // "CI" in ASCII

    public const ushort CPFileSignature = 0x5043; // "CP" in ASCII

    public const ushort ICFileSignature = 0x4349; // "IC" in ASCII

    public const ushort PTFileSignature = 0x5450; // "PT" in ASCII

    public ushort FileSignature
    {
        get => Binary.ReadU16(Array, Offset, Binary.IsLittleEndian);
        set => Binary.Write16(Array, Offset, Binary.IsLittleEndian, value);
    }

    public uint FileSize
    {
        get => Binary.ReadU32(Array, Offset + 2, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset + 2, Binary.IsBigEndian, value);
    }

    public uint Reserved
    {
        get => Binary.ReadU32(Array, Offset + 6, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset + 6, Binary.IsBigEndian, value);
    }

    public uint DataOffset
    {
        get => Binary.ReadU32(Array, Offset + 10, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset + 10, Binary.IsBigEndian, value);
    }

    public BitmapHeader()
        : this(new byte[Length], 0)
    {
    }

    public BitmapHeader(byte[] array, int offset)
       : base(array, offset, Length)
    {
    }
}
