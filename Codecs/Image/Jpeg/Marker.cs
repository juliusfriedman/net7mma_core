using Media.Common;

namespace Media.Codec.Jpeg;

//Needs to implement a common class if the elements can be reused => 
public class Marker : MemorySegment
{
    public const int LengthBytes = 2;

    public byte Prefix
    {
        get => Array[Offset];
        set => Array[Offset] = value;
    }

    public byte FunctionCode
    {
        get => Array[Offset + 1];
        set => Array[Offset + 1] = value;
    }

    public int Length
    {
        get => Binary.ReadU16(Array, Offset + 2, Binary.IsLittleEndian);
        set => Binary.Write16(Array, Offset + 2, Binary.IsLittleEndian, (ushort)value);
    }

    public int DataSize => Binary.Max(0, Length - LengthBytes);

    public MemorySegment Data => DataSize > 0 ? new MemorySegment(Array, Offset + Binary.BytesPerInteger, DataSize) : Empty;

    public bool IsEmpty => DataSize == 0;

    public Marker(byte functionCode, int size)
        : base(new byte[size > 0 ? size + Binary.BytesPerInteger : size + LengthBytes])
    {
        Prefix = Markers.Prefix;
        FunctionCode = functionCode;
        if (size > 0)
            Length = size;
    }

    public Marker(MemorySegment data): base(data)
    {
    }

    public override string ToString()
        => Markers.ToTextualConvention(FunctionCode);
}
