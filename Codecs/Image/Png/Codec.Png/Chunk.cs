using Media.Common;

namespace Codec.Png;

public class Chunk : MemorySegment
{
    public Chunk(byte[] array, int offset)
        : base(array, offset)
    {
    }

    public Chunk(string chunkType, int chunkSize)
        : base(new MemorySegment(ChunkHeader.ChunkHeaderLength + Binary.BytesPerInteger + chunkSize))
    {
        ChunkType = chunkType;
        ChunkSize = chunkSize;
    }

    public ChunkHeader Header => new ChunkHeader(Array, Offset);

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
        ChunkHeader header = new ChunkHeader();
        if (ChunkHeader.ChunkHeaderLength != inputStream.Read(header.Array, header.Offset, ChunkHeader.ChunkHeaderLength))
            throw new InvalidDataException("Not enough bytes for chunk length.");
        var chunk = new Chunk(header.Name, (int)header.Length);
        if (header.Length != inputStream.Read(chunk.Array, chunk.DataOffset, (int)header.Length))
            throw new InvalidDataException("Not enough bytes for chunk data.");
        if (Binary.BytesPerInteger != inputStream.Read(chunk.Array, chunk.CrcDataOffset, Binary.BytesPerInteger))
            throw new InvalidDataException("Not enough bytes for CrcData.");
        return chunk;
    }
}
