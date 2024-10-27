using Codec.Jpeg.Classes;
using Media.Common;
using System.Collections.Generic;

namespace Media.Codec.Jpeg.Segments;

public class QuantizationTables : Marker, IEnumerable<QuantizationTable>
{
    public QuantizationTables(int size) 
        : base(Markers.QuantizationTable, LengthBytes + size)
    {
    }

    public QuantizationTables(MemorySegment segment)
        : base(segment)
    {
    }

    /// <summary>
    /// Gets any contained <see cref="QuantizationTable"/>
    /// </summary>
    internal IEnumerable<QuantizationTable> Tables
    {
        get
        {
            var offset = DataOffset;
            while (offset < MarkerLength)
            {
                using var result = new QuantizationTable(this.Slice(offset, 64));
                offset += result.TotalLength;
                yield return result;
            }
        }
        set
        {
            var offset = DataOffset;
            foreach (var table in value)
            {
                table.CopyTo(Array, offset);
                offset += table.Count;
            }
        }
    }

    IEnumerator<QuantizationTable> IEnumerable<QuantizationTable>.GetEnumerator()
    {
        return Tables.GetEnumerator();
    }
}