using Codec.Jpeg.Classes;
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
        //get
        //{
        //    byte[] bits = new byte[HuffmanTable.Length + HuffmanTable.CodeLength];
        //    byte[] huffval = new byte[byte.MaxValue + 1];
        //    var length = MarkerLength;
        //    int offset = DataOffset;
        //    while (length > HuffmanTable.Length + HuffmanTable.CodeLength)
        //    {
        //        byte index = Array[offset++];

        //        int count = 0;

        //        for (int i = HuffmanTable.Length; i <= HuffmanTable.CodeLength; i++)
        //        {
        //            byte temp = Array[offset++];

        //            bits[i] = temp;
        //            count += temp;
        //        }

        //        length -= HuffmanTable.Length + HuffmanTable.CodeLength;

        //        Buffer.BlockCopy(Array, offset, huffval, 0, count);

        //        offset += count;

        //        length -= count;

        //        using var result = new HuffmanTable(index, bits, huffval, count);

        //        using var slice = this.Slice(
        //            offset - count - HuffmanTable.CodeLength,
        //            count + HuffmanTable.CodeLength);

        //        slice.CopyTo(result.Array, result.Offset + HuffmanTable.Length);

        //        yield return result;

        //        System.Array.Clear(bits, 0, bits.Length); // Clear bits array
        //        System.Array.Clear(huffval, 0, huffval.Length); // Clear huffval array
        //    }
        //}
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