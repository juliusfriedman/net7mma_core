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

    public MemorySegment Data => new MemorySegment(Array, Offset + Binary.BytesPerInteger, DataSize);

    public Marker(byte functionCode, int size)
        : base(new byte[size + Binary.BytesPerInteger])
    {
        Prefix = Markers.Prefix;
        FunctionCode = functionCode;
        Length = size;
    }
}
