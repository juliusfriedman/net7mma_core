using Media.Common;

namespace Media.Codec.Png;

public class Chunk : MemorySegment
{
    public Chunk(ChunkHeader header)
        : base(new MemorySegment(ChunkHeader.ChunkHeaderLength + Binary.BytesPerInteger + header.Length))
    {
        Header = header;
    }

    public Chunk(string chunkType, int chunkSize)
        : base(new MemorySegment(ChunkHeader.ChunkHeaderLength + Binary.BytesPerInteger + chunkSize))
    {
        ChunkType = chunkType;
        ChunkSize = chunkSize;
    }

    public Chunk(uint chunkType, int chunkSize)
       : base(new MemorySegment(ChunkHeader.ChunkHeaderLength + Binary.BytesPerInteger + chunkSize))
    {
        RawType = chunkType;
        ChunkSize = chunkSize;
    }

    public ChunkHeader Header
    {
        get => new ChunkHeader(Array, Offset);
        set
        {
            RawType = value.Type;
            ChunkSize = (int)value.Length;
        }
    }

    /// <summary>
    /// Number of bytes contained including the <see cref="Crc"/>
    /// </summary>
    public int TotalLength => (int)(Header.Length + Binary.BytesPerInteger);

    /// <summary>
    /// The number of bytes contained in the <see cref="Data"/> segment.
    /// </summary>
    public int ChunkSize
    {
        get { return (int)Header.Length; }
        set { Header.Length = (uint)value; }
    }

    public string ChunkType
    {
        get { return Header.Name; }
        set { Header.Name = value; }
    }

    public uint RawType
    {
        get => Header.Type;
        set => Header.Type = value;
    }

    public ChunkNames ChunkName 
    {
        get => (ChunkNames)RawType;
        set => RawType = (uint)value;
    }

    /// <summary>
    /// The offset at which <see cref="Data"/> begins
    /// </summary>
    public int DataOffset => Offset + ChunkHeader.ChunkHeaderLength;

    public MemorySegment Data => new MemorySegment(Array, DataOffset, (int)Header.Length);

    public int Crc
    {
        get { return Binary.Read32(Array, Offset + ChunkHeader.ChunkHeaderLength + ChunkSize, Binary.IsBigEndian); }
        set { Binary.Write32(Array, Offset + ChunkHeader.ChunkHeaderLength + ChunkSize, Binary.IsBigEndian, value); }
    }

    public int CrcDataOffset => Offset + ChunkHeader.ChunkHeaderLength + ChunkSize;

    public MemorySegment CrcData => new(Array, CrcDataOffset, Binary.BytesPerInteger);

    internal static Chunk ReadChunk(Stream inputStream)
    {
        using ChunkHeader header = new ChunkHeader();
        if (ChunkHeader.ChunkHeaderLength != inputStream.Read(header.Array, header.Offset, ChunkHeader.ChunkHeaderLength))
            throw new InvalidDataException("Not enough bytes for chunk header.");
        var chunk = new Chunk(header);
        if (chunk.TotalLength != inputStream.Read(chunk.Array, chunk.DataOffset, chunk.TotalLength))
            throw new InvalidDataException("Not enough bytes for chunk data.");
        return chunk;
    }
}
