using Codec.Jpeg.Classes;
using Media.Codec.Jpeg;
using Media.Common;
using System.Collections.Generic;

namespace Codec.Jpeg.Markers;

public class StartOfFrame : Marker
{
    /// <summary>
    /// The minimum size of the marker without any components
    /// </summary>
    public new const int Length = 6;

    /// <summary>
    /// Sample Precision – Specifies the precision in bits for the samples of the components in the frame
    /// </summary>
    public int P
    {
        get => Array[DataOffset];
        set => Array[DataOffset] = (byte)value;
    }

    /// <summary>
    /// Number of lines – Specifies the maximum number of lines in the source image
    /// </summary>
    public int Y
    {
        get => Binary.ReadU16(Array, DataOffset + 1, Binary.IsLittleEndian);
        set => Binary.Write16(Array, DataOffset + 1, Binary.IsLittleEndian, (ushort)value);
    }

    /// <summary>
    /// Number of samples per line – Specifies the maximum number of samples per line in the source image
    /// </summary>
    public int X
    {
        get => Binary.ReadU16(Array, DataOffset + 3, Binary.IsLittleEndian);
        set => Binary.Write16(Array, DataOffset + 3, Binary.IsLittleEndian, (ushort)value);
    }

    /// <summary>
    ///  Number of <see cref="FrameComponent"/>s components in frame
    /// </summary>
    public int Nf
    {
        get => Binary.ReadU8(Array, DataOffset + 5, Binary.IsBigEndian);
        set => Binary.Write8(Array, DataOffset + 5, Binary.IsBigEndian, (byte)value);
    }

    /// <summary>
    /// Gets or Sets a <see cref="FrameComponent"/> by index.
    /// </summary>
    /// <param name="index">The index</param>
    /// <returns></returns>
    public new FrameComponent this[int index]
    {
        get
        {
            var offset = DataOffset + Length + index * FrameComponent.Length;
            using var slice = this.Slice(offset, FrameComponent.Length);
            return new FrameComponent(slice);
        }
        set
        {
            var offset = DataOffset + Length + index * FrameComponent.Length;
            using var slice = this.Slice(offset, FrameComponent.Length);
            value.CopyTo(slice);
        }
    }

    /// <summary>
    /// Gets the <see cref="FrameComponent"/>s contained in the frame. (Should be equal to <see cref="Nf"/>)
    /// </summary>
    public IEnumerable<FrameComponent> Components
    {
        get
        {
            for (int nf = Nf, j = 0; j < nf; ++j)
            {
                yield return this[j];
            }
        }
    }

    /// <summary>
    /// Create a new instance of the <see cref="StartOfFrame"/> class.
    /// </summary>
    /// <param name="functionCode"></param>
    /// <param name="componentCount">How many components this <see cref="Marker"/> should hold. (Multiples of <see cref="FrameComponent.Length"/>)</param>
    public StartOfFrame(byte functionCode, int componentCount)
        : base(functionCode, LengthBytes + Length + componentCount * FrameComponent.Length)
    {
        Nf = componentCount;
    }

    public StartOfFrame(MemorySegment data)
        : base(data)
    {
    }
}
