using Media.Codec.Png;
using Media.Common;

namespace Media.Codec.Png.Chunks;

public class Header : Chunk
{
    public const int Length = 13;

    public Header()
        : base(ChunkName.Header, Length)
    {
    }

    public Header(Chunk chunk)
        : base(chunk)
    {
    }

    public int Width
    {
        get => Binary.Read32(this, DataOffset, Binary.IsLittleEndian);
        set => Binary.Write32(Array, DataOffset, Binary.IsLittleEndian, value);
    }

    public int Height
    {
        get => Binary.Read32(this, DataOffset + Binary.BytesPerInteger, Binary.IsLittleEndian);
        set => Binary.Write32(Array, DataOffset + Binary.BytesPerInteger, Binary.IsLittleEndian, value);
    }

    public byte BitDepth
    {
        get => this[DataOffset + Binary.BytesPerLong];
        set => this[DataOffset + Binary.BytesPerLong] = value;
    }

    public byte ColourType
    {
        get => this[DataOffset + Binary.BytesPerLong + 1];
        set => this[DataOffset + Binary.BytesPerLong + 1] = value;
    }

    public byte CompressionMethod
    {
        get => this[DataOffset + Binary.BytesPerLong + 2];
        set => this[DataOffset + Binary.BytesPerLong + 2] = value;
    }

    public byte FilterMethod
    {
        get => this[DataOffset + Binary.BytesPerLong + 3];
        set => this[DataOffset + Binary.BytesPerLong + 3] = value;
    }

    public byte InterlaceMethod
    {
        get => this[DataOffset + Binary.BytesPerLong + 4];
        set => this[DataOffset + Binary.BytesPerLong + 4] = value;
    }
}
