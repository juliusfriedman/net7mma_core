using Media.Common;
using System;
using System.Collections.Generic;

namespace Media.Codec.Jpeg;

public class HuffmanTable : Marker
{
    private readonly Dictionary<int, (int code, int length)> _codeTable = new();

    public new const int Length = 1;

    public HuffmanTable(int size) : base(Markers.HuffmanTable, LengthBytes + Length + size)
    {
    }

    public HuffmanTable(MemorySegment segment)
        : base(segment)
    {
        PopulateCodeTable();
    }

    private void PopulateCodeTable()
    {
        //int code = 0;
        //using var li = Li;
        //using var vi = Vi;

        //for (int length = 1; length <= 16; length++)
        //{
        //    int numCodes = li[length - 1];
        //    for (int i = 0; i < numCodes; i++)
        //    {
        //        int value = vi[code];
        //        _codeTable[value] = (code, length);
        //        code++;
        //    }
        //    code <<= 1; // Move to the next bit length
        //}
    }

    /// <summary>
    /// Table class, 0 = DC table or lossless table, 1 = AC table
    /// Baseline 0 or 1, Progressive DCT or Lossless = 0
    /// </summary>
    public int Te
    {
        get
        {
            var bitOffset = Binary.BytesToBits(DataOffset);
            return (int)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(DataOffset);
            this.WriteBits(bitOffset, Binary.Four, (uint)value, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// Huffman table destination identifier, (one of four possible destination identifiers).
    /// Baseline 0 or 1, Extended 0 to 3
    /// </summary>
    public int Th
    {
        get
        {
            var bitOffset = Binary.BytesToBits(DataOffset) + Binary.Four;
            return (int)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(DataOffset) + Binary.Four;
            this.WriteBits(bitOffset, Binary.Four, (uint)value, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// Number of Huffman codes of length i bits (1-16)
    /// </summary>
    public MemorySegment Li
    {
        get => this.Slice(DataOffset + 1, 16);
        set => value.CopyTo(Array, DataOffset + 1, 16);
    }

    /// <summary>
    /// Values associated with each Huffman code of length i bits (1-16)
    /// </summary>
    public MemorySegment Vi
    {
        get => this.Slice(DataOffset + 1 + 16);
        set => value.CopyTo(Array, DataOffset + 1 + 16);
    }

    public int GetCodeLength(int code)
    {
        int codeIndex = 0;

        // Read the code from the Vi property
        using var vi = Vi;
        using var li = Li;

        // Traverse the Huffman table to find the length of the code that matches the given value
        for (int length = 1; length <= 16; length++)
        {
            for (int i = 0; i < li[length - 1]; i++)
            {
                if (vi[codeIndex] == code)
                {
                    return length;
                }
                codeIndex++;
            }
        }

        return 0;
    }

    public int GetCode(int codeLength)
    {
        // Ensure the code length is within the valid range
        if (codeLength < 1 || codeLength > 16)
        {
            return 0;
        }

        // Read the number of codes for each length from the Li property
        using var li = Li;
        int codeIndex = 0;

        // Traverse the Huffman table to find the code that matches the given length
        for (int i = 0; i < codeLength; i++)
        {
            codeIndex += li[i];
        }

        // Read the code from the Vi property
        using var vi = Vi;
        return vi[codeIndex];
    }
}