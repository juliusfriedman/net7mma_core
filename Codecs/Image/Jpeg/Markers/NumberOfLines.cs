using Media.Codec.Jpeg;
using Media.Common;

namespace Codec.Jpeg.Markers;
public class NumberOfLines : Marker
{
    public new const int Length = 2;

    /// <summary>
    /// Number of lines
    /// </summary>
    public int Nl
    {
        get => Binary.Read16(Array, Data.Offset + 2, Binary.IsLittleEndian);
        set => Binary.Write16(Array, Data.Offset + 2, Binary.IsLittleEndian, (ushort)value);
    }

    public NumberOfLines(byte functionCode, int numberOfLines)
        : base(functionCode, Length)
    {
        Nl = numberOfLines;
    }

    public NumberOfLines(MemorySegment data)
        : base(data)
    {
    }
}