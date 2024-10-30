using Media.Common;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Media.Codec.Jpeg.Classes;

internal class QuantizationTable : MemorySegment
{
    #region Static Methods

    public static Block ScaleQuantizationTable(int scale, ReadOnlySpan<byte> unscaledTable)
    {
        Block table = new();
        for (int j = 0; j < table.FloatLength; j++)
        {
            int x = ((unscaledTable[j] * scale) + 50) / 100;
            table[j] = (short)Binary.Clamp(x, 1, 255);
        }

        return table;
    }

    public static Block ScaleQuantizationTable(int quality, Block unscaledTable)
    {
        int scale = QualityToScale(quality);
        Block table = new();
        for (int j = 0; j < table.FloatLength; j++)
        {
            int x = ((unscaledTable[j] * scale) + 50) / 100;
            table[j] = (short)(uint)Binary.Clamp(x, 1, 255);
        }

        return table;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int QualityToScale(int quality)
    {
        return quality < 50 ? (5000 / quality) : (200 - (quality * 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Block ScaleLuminanceTable(int quality)
       => ScaleQuantizationTable(scale: QualityToScale(quality), JpegCodec.DefaultChrominanceQuantTable);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Block ScaleChrominanceTable(int quality)
        => ScaleQuantizationTable(scale: QualityToScale(quality), JpegCodec.DefaultLuminanceQuantTable);

    internal static QuantizationTable CreateQuantizationTable(int pq, int tq, int quality, QuantizationTableType tableType)
    {
        if (quality < 1 || quality > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(quality), "Quality must be between 1 and 100.");
        }

        var baseTable = tableType == QuantizationTableType.Luminance
            ? JpegCodec.DefaultLuminanceQuantTable
            : JpegCodec.DefaultChrominanceQuantTable;

        var length = pq == 0 ? 64 : 128;

        QuantizationTable result;

        if (quality == 50)
        {
            result = new QuantizationTable(pq, tq);
            using (var qk = result.Qk)
            {
                if (pq == 0)
                {
                    baseTable.CopyTo(qk.ToSpan());
                }
                else
                {
                    int offset = qk.Offset;
                    foreach (var q in baseTable)
                    {
                        Binary.Write16(qk.Array, ref offset, Binary.IsLittleEndian, q);
                    }
                }

                return result;
            }
        }

        int scaleFactor = quality < 50 ? 5000 / quality : 200 - quality * 2;

        Span<byte> quantizationTable = stackalloc byte[length];

        for (int i = 0; i < length; i++)
        {
            int value = (baseTable[i] * scaleFactor + 50) / 100;
            quantizationTable[i] = (byte)Binary.Clamp(value, 1, 255);
        }

        result = new QuantizationTable(pq, tq);

        using (var qk = result.Qk)
        {
            if (pq == 0)
            {
                quantizationTable.CopyTo(qk.ToSpan());
            }
            else
            {
                int offset = qk.Offset;
                foreach (var q in baseTable)
                {
                    Binary.Write16(result.Array, ref offset, Binary.IsLittleEndian, q);
                }
            }
            return result;
        }
    }

    #endregion

    /// <summary>
    /// The length of <see cref="Pq"/> and <see cref="Tq"/>
    /// </summary>
    public const int Length = 1;

    public QuantizationTable(int pq, int tq) 
        : base(Length + (pq == 0 ? 64 : 128))
    {
        Pq = pq;
        Tq = tq;
    }

    public QuantizationTable(MemorySegment segment) 
        : base(segment)
    {

    }

    /// <summary>
    ///  Quantization table element precision – Specifies the precision of the Qk values. 
    ///  Value 0 indicates 8-bit Qk values; value 1 indicates 16-bit Qk values.
    /// </summary>
    public int Pq
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Offset);
            return (int)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(Offset);
            this.WriteBits(bitOffset, Binary.Four, (uint)value, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// Quantization table destination identifier.
    /// Specifies one of four possible destinations at the decoder into which the quantization table shall be installed.
    /// </summary>
    public int Tq
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Offset) + Binary.Four;
            return (int)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(Offset) + Binary.Four;
            this.WriteBits(bitOffset, Binary.Four, (uint)value, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    ///  Quantization table elements.
    ///  Specifies the kth element out of 64 elements, where k is the index in the zigzag ordering of the DCT coefficients.
    ///  The quantization elements shall be specified in zig-zag scan order.
    /// </summary>
    public MemorySegment Qk
    {
        get => this.Slice(Length, TableLength);
        set => value.CopyTo(Array, Offset + TableLength);
    }

    /// <summary>
    /// The amount of bytes contained in the <see cref="Qk"/> segment.
    /// </summary>
    public int TableLength => Pq == 0 ? 64 : 128;

    /// <summary>
    /// The total amount of bytes contained.
    /// </summary>
    public int TotalLength => Length + TableLength;

    /// <summary>
    /// Gets a span of the table data.
    /// </summary>
    /// <returns></returns>
    public Span<byte> GetTableData()
    {
        using var qk = Qk;
        return qk.ToSpan();
    }

    /// <summary>
    /// Gets a span of the table data as 16-bit values.
    /// </summary>
    /// <returns></returns>
    public Span<short> GetTableData16()
    {
        using var qk = Qk;
        var span = qk.ToSpan();
        return MemoryMarshal.Cast<byte, short>(span);
    }

    /// <summary>
    /// Returns the <see cref="Qk"/> segment as a <see cref="Block"/>
    /// </summary>
    /// <returns></returns>
    public Block AsBlock()
    {
        return new Block(Qk);
    }
}
