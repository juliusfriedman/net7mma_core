using Media.Codec.Jpeg;
using Media.Common;
using System;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;

namespace Media.Codec.Jpeg.Classes;

//Based on:
//https://github.com/SixLabors/ImageSharp/blob/main/src/ImageSharp/Formats/Jpeg/Components/Block8x8.cs
//https://github.com/SixLabors/ImageSharp/blob/main/src/ImageSharp/Formats/Jpeg/Components/Block8x8F.cs

/// <summary>
/// Represents a block of data, typically 64 coeffients long. (256 bytes)
/// </summary>
internal class Block : MemorySegment
{
    public const int DefaultSize = JpegCodec.BlockSize * JpegCodec.BlockSize;

    #region Static Functions

    /// <summary>
    /// Calculate the total sum of absolute differences of elements in 'a' and 'b'.
    /// </summary>
    public static long TotalDifference(ref Block a, ref Block b)
    {
        long result = 0;

        for (int i = 0; i < DefaultSize; i++)
        {
            int d = a[i] - b[i];
            result += Math.Abs(d);
        }

        return result;
    }

    /// <summary>
    /// Loads a block from a span of short data
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static Block Load(Span<short> data)
    {
        var block = new Block();
        var bytes = MemoryMarshal.Cast<short, byte>(data);
        bytes.CopyTo(block.Array);
        return block;
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a block with the <see cref="DefaultSize"/> of coefficients.
    /// </summary>
    public Block() 
        : base(DefaultSize * Binary.BytesPerInteger)
    {

    }

    /// <summary>
    /// Specifies the count of coefficients in the block.
    /// </summary>
    /// <param name="coefficientCount"></param>
    public Block(int coefficientCount)
        : base(coefficientCount * Binary.BytesPerInteger)
    {

    }

    #endregion

    #region Vector Properties

    public Vector128<short> V0
    {
        get => Unsafe.ReadUnaligned<Vector128<short>>(ref Array[Offset]);
        set => Unsafe.WriteUnaligned(ref Array[Offset], value);
    }

    public Vector128<short> V1
    {
        get => Unsafe.ReadUnaligned<Vector128<short>>(ref Array[Offset + 16]);
        set => Unsafe.WriteUnaligned(ref Array[Offset + 16], value);
    }

    public Vector128<short> V2
    {
        get => Unsafe.ReadUnaligned<Vector128<short>>(ref Array[Offset + 32]);
        set => Unsafe.WriteUnaligned(ref Array[Offset + 32], value);
    }

    public Vector128<short> V3
    {
        get => Unsafe.ReadUnaligned<Vector128<short>>(ref Array[Offset + 48]);
        set => Unsafe.WriteUnaligned(ref Array[Offset + 48], value);
    }

    public Vector128<short> V4
    {
        get => Unsafe.ReadUnaligned<Vector128<short>>(ref Array[Offset + 64]);
        set => Unsafe.WriteUnaligned(ref Array[Offset + 64], value);
    }

    public Vector128<short> V5
    {
        get => Unsafe.ReadUnaligned<Vector128<short>>(ref Array[Offset + 80]);
        set => Unsafe.WriteUnaligned(ref Array[Offset + 80], value);
    }

    public Vector128<short> V6
    {
        get => Unsafe.ReadUnaligned<Vector128<short>>(ref Array[Offset + 96]);
        set => Unsafe.WriteUnaligned(ref Array[Offset + 96], value);
    }

    public Vector128<short> V7
    {
        get => Unsafe.ReadUnaligned<Vector128<short>>(ref Array[Offset + 112]);
        set => Unsafe.WriteUnaligned(ref Array[Offset + 112], value);
    }

    public Vector256<short> V01
    {
        get => Unsafe.ReadUnaligned<Vector256<short>>(ref Array[Offset]);
        set => Unsafe.WriteUnaligned(ref Array[Offset], value);
    }

    public Vector256<short> V23
    {
        get => Unsafe.ReadUnaligned<Vector256<short>>(ref Array[Offset + 32]);
        set => Unsafe.WriteUnaligned(ref Array[Offset + 32], value);
    }

    public Vector256<short> V45
    {
        get => Unsafe.ReadUnaligned<Vector256<short>>(ref Array[Offset + 64]);
        set => Unsafe.WriteUnaligned(ref Array[Offset + 64], value);
    }

    public Vector256<short> V67
    {
        get => Unsafe.ReadUnaligned<Vector256<short>>(ref Array[Offset + 96]);
        set => Unsafe.WriteUnaligned(ref Array[Offset + 96], value);
    }

    public Vector4 V0L
    {
        get => new Vector4(GetFourFloats(0));
        set => value.CopyTo(GetFourFloats(0));
    }

    public Vector4 V0R
    {
        get => new Vector4(GetFourFloats(16));
        set => value.CopyTo(GetFourFloats(16));
    }

    public Vector4 V1L
    {
        get => new Vector4(GetFourFloats(32));
        set => value.CopyTo(GetFourFloats(32));
    }

    public Vector4 V1R
    {
        get => new Vector4(GetFourFloats(48));
        set => value.CopyTo(GetFourFloats(48));
    }

    public Vector4 V2L
    {
        get => new Vector4(GetFourFloats(64));
        set => value.CopyTo(GetFourFloats(64));
    }

    public Vector4 V2R
    {
        get => new Vector4(GetFourFloats(80));
        set => value.CopyTo(GetFourFloats(80));
    }

    public Vector4 V3L
    {
        get => new Vector4(GetFourFloats(96));
        set => value.CopyTo(GetFourFloats(96));
    }

    public Vector4 V3R
    {
        get => new Vector4(GetFourFloats(112));
        set => value.CopyTo(GetFourFloats(112));
    }

    public Vector4 V4L
    {
        get => new Vector4(GetFourFloats(128));
        set => value.CopyTo(GetFourFloats(128));
    }

    public Vector4 V4R
    {
        get => new Vector4(GetFourFloats(144));
        set => value.CopyTo(GetFourFloats(144));
    }

    public Vector4 V5L
    {
        get => new Vector4(GetFourFloats(160));
        set => value.CopyTo(GetFourFloats(160));
    }

    public Vector4 V5R
    {
        get => new Vector4(GetFourFloats(176));
        set => value.CopyTo(GetFourFloats(176));
    }

    public Vector4 V6L
    {
        get => new Vector4(GetFourFloats(192));
        set => value.CopyTo(GetFourFloats(192));
    }

    public Vector4 V6R
    {
        get => new Vector4(GetFourFloats(208));
        set => value.CopyTo(GetFourFloats(208));
    }

    public Vector4 V7L
    {
        get => new Vector4(GetFourFloats(224));
        set => value.CopyTo(GetFourFloats(224));
    }

    public Vector4 V7R
    {
        get => new Vector4(GetFourFloats(240));
        set => value.CopyTo(GetFourFloats(240));
    }

    #endregion

    #region Properties
    
    public int ShortLength => Count / Binary.BytesPerShort;

    public int FloatLength => Count / Binary.BytesPerInteger;

    #endregion

    #region Indexers

    /// <summary>
    /// Gets or sets a <see cref="short"/> value at the given index
    /// </summary>
    /// <param name="idx">The index</param>
    /// <returns>The value</returns>
    public new short this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ref short selfRef = ref Unsafe.As<byte, short>(ref Array[Offset + index * Binary.BytesPerShort]);
            return selfRef;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            ref short selfRef = ref Unsafe.As<byte, short>(ref Array[Offset + index * Binary.BytesPerShort]);
            selfRef = value;
        }
    }

    /// <summary>
    /// Gets or sets a value in a row and column of the block
    /// </summary>
    /// <param name="x">The x position index in the row</param>
    /// <param name="y">The column index</param>
    /// <returns>The value</returns>
    public short this[int x, int y]
    {
        get => this[(y * JpegCodec.BlockSize) + x];
        set => this[(y * JpegCodec.BlockSize) + x] = value;
    }

    /// <summary>
    /// Gets or sets a <see cref="float"/> value at the given index
    /// </summary>
    /// <param name="idx"></param>
    /// <returns></returns>
    public float this[nuint index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ref float selfRef = ref Unsafe.As<byte, float>(ref Array[(nuint)Offset + index * Binary.BytesPerInteger]);
            return selfRef;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            ref float selfRef = ref Unsafe.As<byte, float>(ref Array[(nuint)Offset + index * Binary.BytesPerInteger]);
            selfRef = value;
        }
    }

    /// <summary>
    /// Gets or sets a <see cref="float"/> value at the given index
    /// </summary>
    /// <param name="idx"></param>
    /// <returns></returns>
    public float this[uint index]
    {
        get => this[(nuint)index];
        set => this[(nuint)index] = value;
    }

    /// <summary>
    /// Gets or sets a value in a row and column of the block
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public float this[uint x, uint y]
    {
        get => this[(y * JpegCodec.BlockSize) + x];
        set => this[(y * JpegCodec.BlockSize) + x] = value;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets a <see cref="Span{float}"/> of length 4 in the block at the given index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    internal Span<float> GetFourFloats(int index)
    {
        var span = this.ToSpan();
        return MemoryMarshal.Cast<byte, float>(MemoryMarshal.CreateSpan(ref span[index], Binary.BytesPerInteger * Binary.Four));
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0, e = ShortLength; i < e; i++)
        {
            sb.Append(this[i]);
            if (i < e - 1)
            {
                sb.Append(',');
            }
        }
        sb.Append(']');
        return sb.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
     => obj is Block block && Equals(block);

    /// <summary>
    /// Determines if the current instance is equal to the other instance
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Equals(Block other)
     => V0L == other.V0L
        && V0R == other.V0R
        && V1L == other.V1L
        && V1R == other.V1R
        && V2L == other.V2L
        && V2R == other.V2R
        && V3L == other.V3L
        && V3R == other.V3R
        && V4L == other.V4L
        && V4R == other.V4R
        && V5L == other.V5L
        && V5R == other.V5R
        && V6L == other.V6L
        && V6R == other.V6R
        && V7L == other.V7L
        && V7R == other.V7R;

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int left = HashCode.Combine(
            V0L,
            V1L,
            V2L,
            V3L,
            V4L,
            V5L,
            V6L,
            V7L);

        int right = HashCode.Combine(
            V0R,
            V1R,
            V2R,
            V3R,
            V4R,
            V5R,
            V6R,
            V7R);

        return HashCode.Combine(left, right);
    }

    #endregion
}
