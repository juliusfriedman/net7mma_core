using Codec.Jpeg.Classes;
using Media.Codec.Jpeg;
using Media.Common;
using System.Collections.Generic;

namespace Codec.Jpeg.Markers;

public sealed class StartOfScan : Marker
{
    /// <summary>
    /// The number of bytes in the <see cref="Data"/> segment of this marker when <see cref="Ns"/> is 0.
    /// </summary>
    public new const int Length = 4;

    public StartOfScan(int numberOfComponentSelectors) 
        : base(Media.Codec.Jpeg.Markers.StartOfScan, Length + numberOfComponentSelectors * ScanComponentSelector.Length) 
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
        get => Array[DataOffset];
        set => Array[DataOffset] = (byte)value;
    }

    /// <summary>
    /// Get the <see cref="ScanComponentSelector"/>s contained in the scan. (Should be equal to <see cref="Ns"/>)
    /// </summary>
    public IEnumerable<ScanComponentSelector> Components
    {
        get
        {
            //Read the Scan component selectors (Csj)
            for (int ns = Ns, j = 0; j < ns; ++j)
            {
                yield return this[j];
            }
        }
    }

    /// <summary>
    /// Gets or sets a <see cref="ScanComponentSelector"/> based on the given index.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public new ScanComponentSelector this[int index]
    {
        get
        {
            var offset = index * ScanComponentSelector.Length;
            using var slice = this.Slice(DataOffset + 1 + offset, ScanComponentSelector.Length);
            return new ScanComponentSelector(slice);
        }
        set => value.CopyTo(Array, DataOffset + 1 + (index * ScanComponentSelector.Length));
    }

    /// <summary>
    /// Start of spectral
    /// </summary>
    public int Ss
    {
        get
        {
            var offset = 1 + Ns * ScanComponentSelector.Length;
            return Array[DataOffset + offset];
        }
        set
        {
            var offset = 1 + Ns * ScanComponentSelector.Length;
            Array[DataOffset + offset] = (byte)value;
        }
    }

    /// <summary>
    /// End of spectral
    /// </summary>
    public int Se
    {
        get
        {
            var offset = 1 + Ns * ScanComponentSelector.Length + 1;
            return Array[DataOffset + offset];
        }
        set
        {
            var offset = 1 + Ns * ScanComponentSelector.Length + 1;
            Array[DataOffset + offset] = (byte)value;
        }
    }

    /// <summary>
    /// Successive approximation bit position high
    /// </summary>
    public int Ah
    {
        get
        {
            var bitOffset = Binary.BytesToBits(DataOffset + 1 + Ns * ScanComponentSelector.Length + 1 + 1);
            return (int)this.ReadBits(ref bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(DataOffset + 1 + Ns * ScanComponentSelector.Length + 1 + 1);
            this.WriteBits(ref bitOffset, Binary.Four, value, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// Successive approximation bit position low or point transform
    /// </summary>
    public int Al
    {
        get
        {
            var bitOffset = Binary.BytesToBits(DataOffset + 1 + Ns * ScanComponentSelector.Length + 1 + 1) + Binary.Four;
            return (int)this.ReadBits(ref bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(DataOffset + 1 + Ns * ScanComponentSelector.Length + 1 + 1) + Binary.Four;
            this.WriteBits(ref bitOffset, Binary.Four, value, Binary.BitOrder.MostSignificant);
        }
    }
}
