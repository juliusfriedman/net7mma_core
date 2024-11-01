using Media.Common;

namespace Media.Codec.Png;

public class Chunk : MemorySegment
{
    public const int ChecksumLength = Binary.BytesPerInteger;
    
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

    #region Constructors

    public Chunk(Chunk other)
        : base(other)
    {
    }

    public Chunk(ChunkHeader header)
        : base(new MemorySegment(ChunkHeader.ChunkHeaderLength + Binary.BytesPerInteger + header.Length))
    {
        Header = header;
    }

    public Chunk(string chunkType, int chunkSize)
        : base(new MemorySegment(ChunkHeader.ChunkHeaderLength + Binary.BytesPerInteger + chunkSize))
    {
        ChunkType = chunkType;
        ChunkLength = chunkSize;
    }

    public Chunk(uint chunkType, int chunkSize)
       : base(new MemorySegment(ChunkHeader.ChunkHeaderLength + Binary.BytesPerInteger + chunkSize))
    {
        RawType = chunkType;
        ChunkLength = chunkSize;
    }

    public Chunk(ChunkName chunkName, int chunkSize)
       : base(new MemorySegment(ChunkHeader.ChunkHeaderLength + Binary.BytesPerInteger + chunkSize))
    {
        ChunkName = chunkName;
        ChunkLength = chunkSize;
    }

    public Chunk(ChunkName chunkName, int chunkSize, byte[] data, int offset)
        :base(data, offset)
    {
        ChunkName = chunkName;
        ChunkLength = chunkSize;
    }

    #endregion

    #region Properties

    public ChunkHeader Header
    {
        get => new ChunkHeader(Array, Offset);
        set
        {
            RawType = value.Type;
            ChunkLength = (int)value.Length;
        }
    }

    /// <summary>
    /// Number of bytes contained including the <see cref="Crc"/>
    /// </summary>
    public int TotalLength => ChunkLength + ChecksumLength;

    /// <summary>
    /// The number of bytes contained in the <see cref="Data"/> segment.
    /// </summary>
    public int ChunkLength
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

    public ChunkName ChunkName 
    {
        get => (ChunkName)RawType;
        set => RawType = (uint)value;
    }

    /// <summary>
    /// The offset at which <see cref="Data"/> begins
    /// </summary>
    public int DataOffset => Offset + ChunkHeader.ChunkHeaderLength;

    public MemorySegment Data => new MemorySegment(Array, DataOffset, ChunkLength);

    public int Crc
    {
        get { return Binary.Read32(Array, CrcDataOffset, Binary.IsLittleEndian); }
        set { Binary.Write32(Array, CrcDataOffset, Binary.IsLittleEndian, value); }
    }

    public int CrcDataOffset => DataOffset + ChunkLength;

    public MemorySegment CrcData => new(Array, CrcDataOffset, ChecksumLength);

    #endregion

    public override string ToString()
        => ChunkType;
}
