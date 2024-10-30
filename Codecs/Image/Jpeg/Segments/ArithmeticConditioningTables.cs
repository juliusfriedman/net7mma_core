using Media.Codec.Jpeg.Classes;
using Media.Common;
using System.Collections.Generic;

namespace Media.Codec.Jpeg.Segments;

internal class ArithmeticConditioningTables : Marker, IEnumerable<ArithmeticConditioningTable>
{
    public ArithmeticConditioningTables(int size)
        : base(Markers.ArithmeticConditioning, LengthBytes + size)
    {
    }

    public ArithmeticConditioningTables(MemorySegment segment)
        : base(segment)
    {
    }

    /// <summary>
    /// Gets any contained <see cref="QuantizationTable"/>
    /// </summary>
    internal IEnumerable<ArithmeticConditioningTable> Tables
    {
        get
        {
            var offset = DataOffset;
            while (offset < MarkerLength)
            {
                using var result = new ArithmeticConditioningTable(this.Slice(offset, ArithmeticConditioningTable.Length));
                offset += ArithmeticConditioningTable.Length;
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

    IEnumerator<ArithmeticConditioningTable> IEnumerable<ArithmeticConditioningTable>.GetEnumerator()
    {
        return Tables.GetEnumerator();
    }
}