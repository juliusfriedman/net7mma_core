using Codec.Jpeg.Classes;
using Media.Codec.Jpeg;
using Media.Common;
using System.Collections.Generic;

namespace Codec.Jpeg.Markers;

public class StartOfScan : Marker
{
    /// <summary>
    /// The number of bytes in the <see cref="Data"/> segment of this marker when <see cref="Ns"/> is 0.
    /// </summary>
    public new const int Length = 4;

    public StartOfScan(int numberOfComponentSelectors) 
        : base(Media.Codec.Jpeg.Markers.StartOfScan, Length + numberOfComponentSelectors * ScanComponentSelectorType.Length) 
    {
        Ns = numberOfComponentSelectors;
    }

    public StartOfScan(MemorySegment data)
        : base(data)
    {
    }

    /// <summary>
    /// Number of component selectors.
    /// </summary>
    public int Ns
    {
        get => Array[Data.Offset];
        set => Array[Data.Offset] = (byte)value;
    }

    /// <summary>
    /// Get the <see cref="ScanComponentSelectorType"/>s contained in the scan. (Should be equal to <see cref="Ns"/>)
    /// </summary>
    public IEnumerable<ScanComponentSelectorType> Components
    {
        get
        {
            var offset = 1;

            //Read the Scan component selectors (Csj)
            for (int ns = Ns, j = 0; j < ns; ++j)
            {
                var result = new ScanComponentSelectorType(Data.Slice(offset, ScanComponentSelectorType.Length));

                yield return result;

                offset += ScanComponentSelectorType.Length;
            }
        }
    }

    /// <summary>
    /// Gets or sets a <see cref="ScanComponentSelectorType"/> based on the given index.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public new ScanComponentSelectorType this[int index]
    {
        get
        {
            var offset = 1 + index * ScanComponentSelectorType.Length;
            using var slice = Data.Slice(offset, ScanComponentSelectorType.Length);
            return new ScanComponentSelectorType(slice);
        }
        set
        {
            var offset = 1 + index * ScanComponentSelectorType.Length;
            using var slice = Data.Slice(offset, ScanComponentSelectorType.Length);
            value.CopyTo(slice);
        }
    }

    /// <summary>
    /// Start of spectral
    /// </summary>
    public int Ss
    {
        get
        {
            var offset = 1 + Ns * ScanComponentSelectorType.Length;
            return Data[offset];
        }
        set
        {
            var offset = 1 + Ns * ScanComponentSelectorType.Length;
            Data[offset] = (byte)value;
        }
    }

    /// <summary>
    /// End of spectral
    /// </summary>
    public int Se
    {
        get
        {
            var offset = 1 + Ns * ScanComponentSelectorType.Length + 1;
            return Data[offset];
        }
        set
        {
            var offset = 1 + Ns * ScanComponentSelectorType.Length + 1;
            Data[offset] = (byte)value;
        }
    }

    /// <summary>
    /// Successive approximation bit position high
    /// </summary>
    public int Ah
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Data.Offset + 1 + Ns * ScanComponentSelectorType.Length + 1 + 1);
            return (int)Binary.ReadBits(Data.Array, ref bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(Data.Offset + 1 + Ns * ScanComponentSelectorType.Length + 1 + 1);
            Binary.WriteBits(Data.Array, bitOffset, Binary.Four, value, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// Successive approximation bit position low or point transform
    /// </summary>
    public int Al
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Data.Offset + 1 + Ns * ScanComponentSelectorType.Length + 1 + 1) + Binary.Four;
            return (int)Binary.ReadBits(Data.Array, ref bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(Data.Offset + 1 + Ns * ScanComponentSelectorType.Length + 1 + 1) + Binary.Four;
            Binary.WriteBits(Data.Array, bitOffset, Binary.Four, value, Binary.BitOrder.MostSignificant);
        }
    }
}
