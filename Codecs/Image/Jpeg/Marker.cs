using Media.Common;

namespace Media.Codec.Jpeg;

//Needs to implement a common class if the elements can be reused => 
public class Marker : MemorySegment
{
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
        get => Binary.Read16(Array, Offset + 2, Binary.IsLittleEndian);
        set => Binary.Write16(Array, Offset + 2, Binary.IsLittleEndian, (ushort)value);
    }

    public int DataSize => Binary.Max(0, Length - 2);

    public MemorySegment Data => DataSize > 0 ? new MemorySegment(Array, Offset + Binary.BytesPerInteger, DataSize) : Common.MemorySegment.Empty;

    public Marker(byte functionCode, int size)
        : base(new byte[size > 0 ? size + Binary.BytesPerInteger : size + Binary.BytesPerShort])
    {
        Prefix = Markers.Prefix;
        FunctionCode = functionCode;
        if (size > 0)
            Length = size;
    }
}
