using Media.Common;
using System.Text;

namespace Codec.Png;

public class ChunkHeader : MemorySegment
{
    public const int ChunkHeaderLength = 8;

    public ChunkHeader()
        : this(new byte[ChunkHeaderLength], 0)
    {
    }

    public ChunkHeader(byte[] array, int offset)
        : base(array, offset, ChunkHeaderLength)
    {
    }

    public uint Length
    {
        get => Binary.ReadU32(Array, Offset, Binary.IsLittleEndian);
        set => Binary.Write32(Array, Offset, Binary.IsLittleEndian, value);
    }

    public int TotalLength => (int)(Length + Binary.BytesPerInteger);

    public uint Type
    {
        get => Binary.ReadU32(Array, Offset + Binary.BytesPerInteger, Binary.IsLittleEndian);
        set => Binary.Write32(Array, Offset + Binary.BytesPerInteger, Binary.IsLittleEndian, value);
    }

    public string Name 
    {
        get { return Encoding.ASCII.GetString(Array, Offset + Binary.BytesPerInteger, Binary.BytesPerInteger); }
        set { Encoding.ASCII.GetBytes(value, 0, Binary.BytesPerInteger, Array, Offset + Binary.BytesPerInteger); }
    }

    /// <summary>
    /// Whether the chunk is critical (must be read by all readers) or ancillary (may be ignored).
    /// </summary>
    public bool IsCritical => char.IsUpper(Name[0]);

    /// <summary>
    /// A public chunk is one that is defined in the International Standard or is registered in the list of public chunk types maintained by the Registration Authority. 
    /// Applications can also define private (unregistered) chunk types for their own purposes.
    /// </summary>
    public bool IsPublic => char.IsUpper(Name[1]);

    public bool IsPrivate => char.IsUpper(Name[2]);

    /// <summary>
    /// Whether the (if unrecognized) chunk is safe to copy.
    /// </summary>
    public bool IsSafeToCopy => char.IsUpper(Name[3]);
}
