using Media.Codec.Jpeg.Classes;
using Media.Common;
using System;
using System.Collections.Generic;

namespace Media.Codec.Jpeg.Segments;

public class HuffmanTables : Marker, IEnumerable<HuffmanTable>
{
    public HuffmanTables(int size) : base(Markers.HuffmanTable, LengthBytes + size)
    {
    }

    public HuffmanTables(MemorySegment segment)
        : base(segment)
    {
    }

    public MemorySegment TablesData
    {
        get => this.Slice(DataOffset);
        set => value.CopyTo(Array, DataOffset);
    }

    internal IEnumerable<HuffmanTable> Tables
    {
        get
        {
            int offset = DataOffset;
            while (offset < MarkerLength)
            {
                var huffmanTable = new HuffmanTable(this.Slice(offset, HuffmanTable.Length + HuffmanTable.CodeLength));
                offset += huffmanTable.TotalLength;
                yield return huffmanTable;
            }
        }
        set
        {
            int offset = DataOffset;
            foreach(var huffmanTable in value)
            {
                huffmanTable.CopyTo(Array, offset);
                offset += huffmanTable.TotalLength;
            }
        }
    }

    IEnumerator<HuffmanTable> IEnumerable<HuffmanTable>.GetEnumerator()
    {
        return Tables.GetEnumerator();
    }
}