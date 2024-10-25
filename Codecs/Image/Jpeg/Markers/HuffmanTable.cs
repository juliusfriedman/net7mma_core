using Codec.Jpeg.Classes;
using Media.Common;
using System;
using System.Collections.Generic;

namespace Media.Codec.Jpeg;

public class HuffmanTable : Marker
{
    public new const int Length = 1;

    public HuffmanTable(int size) : base(Markers.HuffmanTable, LengthBytes + Length + size)
    {
    }

    public HuffmanTable(MemorySegment segment)
        : base(segment)
    {
    }

    public MemorySegment TableData
    {
        get => this.Slice(DataOffset + Length);
        set => value.CopyTo(Array, DataOffset + Length);
    }

    internal IEnumerable<DerivedTable> Tables
    {
        get
        {
            var offset = DataOffset;

            while(offset < Count)
            {
                using var slice = this.Slice(offset);
                using var derivedTable = new DerivedTable(slice);
                yield return derivedTable;
                offset += DerivedTable.Length + derivedTable.ValuesCount;
            }
        }
    }
}