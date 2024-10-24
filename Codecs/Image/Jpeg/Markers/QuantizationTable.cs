using Codec.Jpeg.Classes;
using Media.Common;
using System.IO;

namespace Media.Codec.Jpeg;

public class QuantizationTable : Marker
{    
    public new const int Length = 2;

    public QuantizationTable(int size) : base(LengthBytes + Jpeg.Markers.QuantizationTable, size)
    {
    }

    public int Precision
    {
        get => this[DataOffset] >> Binary.Four;
        set => this[DataOffset] = (byte)((this[4] & 0x0f) | (value << 4));
    }

    public int TableId
    {
        get => this[DataOffset] & 0x0f;
        set => this[DataOffset] = (byte)((this[4] & 0xf0) | value);
    }
}
