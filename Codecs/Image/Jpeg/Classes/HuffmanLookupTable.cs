using Media.Codec.Jpeg.Classes;
using Media.Common;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Codec.Jpeg.Classes;

//Based on https://github.com/SixLabors/ImageSharp/blob/21ec18e1b1186c3f1d549269271177d4516b3ba5/src/ImageSharp/Formats/Jpeg/Components/Decoder/HuffmanTable.cs

/// <summary>
/// Represents a Huffman coding table containing basic coding data plus tables for accelerated computation.
/// </summary>
internal class HuffmanLookupTable
{
    #region Fields

    /// <summary>
    /// Memory segment used for temporary storage.
    /// </summary>
    public readonly MemorySegment Workspace = new MemorySegment(new byte[HuffmanTable.CodeLength + HuffmanScan.LookupSize + HuffmanScan.LookupSize * Binary.BytesPerInteger]);

    /// <summary>
    /// Derived from the DHT marker. Contains the symbols, in order of incremental code length.
    /// </summary>
    public readonly byte[] Values = new byte[HuffmanScan.LookupSize];

    /// <summary>
    /// Contains the largest code of length k (0 if none). MaxCode[17] is a sentinel to ensure <see cref="DecodeHuffman"/> terminates.
    /// </summary>
    public readonly ulong[] MaxCode = new ulong[18];

    /// <summary>
    /// Values[] offset for codes of length k  ValOffset[k] = Values[] index of 1st symbol of code length
    /// k, less the smallest code of length k; so given a code of length k, the corresponding symbol is
    /// Values[code + ValOffset[k]].
    /// </summary>
    public readonly int[] ValOffset = new int[19];

    /// <summary>
    /// Contains the length of bits for the given k value.
    /// </summary>
    public readonly byte[] LookaheadSize = new byte[HuffmanScan.LookupSize];

    /// <summary>
    /// Lookahead table: indexed by the next <see cref="HuffmanScan.LookupBits"/> bits of
    /// the input data stream.  If the next Huffman code is no more
    /// than <see cref="HuffmanScan.LookupBits"/> bits long, we can obtain its length and
    /// the corresponding symbol directly from this tables.
    ///
    /// The lower 8 bits of each table entry contain the number of
    /// bits in the corresponding Huffman code, or <see cref="HuffmanScan.LookupBits"/> + 1
    /// if too long.  The next 8 bits of each entry contain the symbol.
    /// </summary>
    public readonly byte[] LookaheadValue = new byte[HuffmanScan.LookupSize];

    #endregion

    /// <summary>
    /// Constructs a <see cref="HuffmanLookupTable"/>
    /// </summary>
    /// <param name="huffmanTable">The <see cref="HuffmanTable"/> to construct from</param>
    /// <exception cref="InvalidDataException"></exception>
    public HuffmanLookupTable(HuffmanTable huffmanTable)
    {
        using var codeLengths = huffmanTable.Li;
        using var values = huffmanTable.Vi;

        var workspaceSpan = Workspace.ToSpan();
        var workspace = MemoryMarshal.Cast<byte, uint>(workspaceSpan);

        Unsafe.CopyBlockUnaligned(ref Values[0], ref values.Array[values.Offset], (uint)values.Count);

        // Generate codes
        uint code = 0;
        int si = 1;
        int p = 0;
        for (int i = 0; i < codeLengths.Count; i++)
        {
            int count = codeLengths[i];
            for (int j = 0; j < count; j++)
            {
                workspace[p++] = code;
                code++;
            }

            // 'code' is now 1 more than the last code used for codelength 'si'
            // in the valid worst possible case 'code' would have the least
            // significant bit set to 1, e.g. 1111(0) +1 => 1111(1)
            // but it must still fit in 'si' bits since no huffman code can be equal to all 1s
            // if last code is all ones, e.g. 1111(1), then incrementing it by 1 would yield
            // a new code which occupies one extra bit, e.g. 1111(1) +1 => (1)1111(0)
            if (code >= (1 << si))
            {
                throw new InvalidDataException("Bad huffman table.");
            }

            code <<= 1;
            si++;
        }

        // Figure F.15: generate decoding tables for bit-sequential decoding
        p = 0;
        for (int j = 0; j < codeLengths.Count; j++)
        {
            var codeLength = codeLengths[j];
            if (codeLength != 0)
            {
                ValOffset[j] = p - (int)workspace[p];
                p += codeLength;
                MaxCode[j] = workspace[p - 1]; // Maximum code of length l
                MaxCode[j] <<= HuffmanScan.RegisterSize - j; // Left justify
                MaxCode[j] |= (1ul << (HuffmanScan.RegisterSize - j)) - 1;
            }
            else
            {
                MaxCode[j] = 0;
            }
        }

        ValOffset[18] = 0;
        MaxCode[17] = ulong.MaxValue; // Ensures huff decode terminates

        // Compute lookahead tables to speed up decoding.
        // First we set all the table entries to HuffmanScan.SlowBits, indicating "too long";
        // then we iterate through the Huffman codes that are short enough and
        // fill in all the entries that correspond to bit sequences starting
        // with that code.
        ref byte lookupSizeRef = ref LookaheadSize[0];
        Unsafe.InitBlockUnaligned(ref lookupSizeRef, HuffmanScan.SlowBits, HuffmanScan.LookupSize);

        //Todo fix this, it doesn't seem to work correctly when codeLengths are adjusted to be exactly 16 bytes.
        //Most implementations use 1 as the starting value for the loops but the memory here is 0 based.
        //Until fixed decoding time will suffer.

        //p = 0;
        //for (int length = 1; length <= HuffmanScan.LookupBits; length++)
        //{
        //    int jShift = HuffmanScan.LookupBits - length;
        //    for (int i = 1, e = codeLengths[length]; i < e; i++, p++)
        //    {
        //        // length = current code's length, p = its index in huffCode[] & Values[].
        //        // Generate left-justified code followed by all possible bit sequences
        //        int lookBits = (int)(workspace[p] << jShift);
        //        for (int ctr = 1 << (HuffmanScan.LookupBits - length); ctr > 0; --ctr)
        //        {
        //            LookaheadSize[lookBits] = (byte)length;
        //            LookaheadValue[lookBits] = Values[p];
        //            lookBits++;
        //        }
        //    }
        //}
    }
}