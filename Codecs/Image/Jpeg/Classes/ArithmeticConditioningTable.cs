using Media.Common;

namespace Media.Codec.Jpeg.Classes;

public class ArithmeticConditioningTable : MemorySegment
{
    /// <summary>
    /// The length of <see cref="Tc"/> and <see cref="Tb"/> and <see cref="Cs"/> in bytes."/>
    /// </summary>
    public const int Length = 2;

    public ArithmeticConditioningTable(int tc, int tb)
        : base(Length)
    {
        Tc = tc;
        Tb = tb;
    }

    public ArithmeticConditioningTable(MemorySegment segment)
        : base(segment)
    {

    }

    /// <summary>
    ///  Table class – 0 = DC table or lossless table, 1 = AC table
    /// </summary>
    public int Tc
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
    /// Arithmetic coding conditioning table destination identifier.
    /// Specifies one of four possible destinations at the decoder into  which the arithmetic coding conditioning table shall be installed.
    /// </summary>
    public int Tb
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
    ///Conditioning table value.
    ///Value in either the AC or the DC (and lossless) conditioning table. A single
    ///value of Cs shall follow each value of Tb.For AC conditioning tables Tc shall be one and Cs shall contain a
    ///value of Kx in the range 1 ≤ Kx ≤ 63. For DC(and lossless) conditioning tables Tc shall be zero and Cs shall
    ///contain two 4-bit parameters, U and L.U and L shall be in the range 0 ≤ L ≤ U ≤ 15 and the value of Cs shall be
    ///L + 16 × U.
    /// </summary>
    public int Cs
    {
        get => Array[Offset + 1];
        set => Array[Offset + 1] = (byte)value;
    }

    /// <summary>
    /// See <see cref="Cs"/> for more information."/>
    /// </summary>
    public int U
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Offset + 1);
            return (int)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(Offset + 1);
            this.WriteBits(bitOffset, Binary.Four, (uint)value, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// See <see cref="Cs"/> for more information."/>
    /// </summary>
    public int L
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Offset + 1) + Binary.Four;
            return (int)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(Offset + 1) + Binary.Four;
            this.WriteBits(bitOffset, Binary.Four, (uint)value, Binary.BitOrder.MostSignificant);
        }
    }
}
