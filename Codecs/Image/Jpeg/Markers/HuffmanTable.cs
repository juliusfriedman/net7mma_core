using Codec.Jpeg.Classes;
using Media.Common;
using System;
using System.Collections.Generic;

namespace Media.Codec.Jpeg;

public class HuffmanTable : Marker
{
    public HuffmanTable(int size) : base(Markers.HuffmanTable, LengthBytes + size)
    {
    }

    public HuffmanTable(MemorySegment segment)
        : base(segment)
    {
    }

    public MemorySegment TableData
    {
        get => this.Slice(DataOffset);
        set => value.CopyTo(Array, DataOffset);
    }

    internal IEnumerable<DerivedTable> Tables
    {
        get
        {
            byte[] bits = new byte[DerivedTable.Length + DerivedTable.CodeLength];
            byte[] huffval = new byte[byte.MaxValue + 1];
            var length = Count;
            int offset = DataOffset;
            while (length > DerivedTable.CodeLength)
            {
                byte index = Array[offset++];

                int count = 0;

                for (int i = DerivedTable.Length; i <= DerivedTable.CodeLength; i++)
                {
                    byte temp = Array[offset++];

                    bits[i] = temp;
                    count += temp;
                }

                length -= DerivedTable.Length + DerivedTable.CodeLength;

                for (int i = 0; i < count; i++)
                {
                    huffval[i] = Array[offset++];
                }

                length -= count;

                var result = new DerivedTable(index, bits, huffval, count);

                using var slice = this.Slice(
                    offset - count - DerivedTable.CodeLength,
                    count + DerivedTable.CodeLength);

                slice.CopyTo(result.Array, result.Offset + DerivedTable.Length);

                yield return result;

                System.Array.Clear(bits, 0, bits.Length); // Clear bits array
                System.Array.Clear(huffval, 0, huffval.Length); // Clear huffval array
            }
        }
        set
        {
            int offset = DataOffset;
            
            foreach(var derviedTable in value)
            {
                derviedTable.CopyTo(Array, offset);
                offset += derviedTable.Count;
            }
        }
    }
}