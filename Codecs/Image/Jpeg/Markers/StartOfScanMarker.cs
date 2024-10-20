using Codec.Jpeg.Classes;
using Media.Codec.Jpeg;
using Media.Common;
using System.Collections.Generic;

namespace Codec.Jpeg.Markers;

public class StartOfScanMarker : Marker
{
    public StartOfScanMarker(int size) 
        : base(Media.Codec.Jpeg.Markers.StartOfScan, size) 
    {
    }

    public StartOfScanMarker(MemorySegment data)
        : base(data)
    {
    }

    /// <summary>
    /// Number of components.
    /// </summary>
    public int Ns
    {
        get => Array[Data.Offset];
        set => Array[Data.Offset] = (byte)value;
    }

    public IEnumerable<ScanComponentSelectorType> Components
    {
        get
        {
            var offset = 1;

            //Read the Scan component selectors (Csj)
            for (int ns = Ns, j = 0; j < ns; ++j)
            {
                var result = new ScanComponentSelectorType(Data.Slice(offset, 2));

                yield return result;

                offset += 2;
            }
        }
    }

    /// <summary>
    /// Start of spectral
    /// </summary>
    public int Ss
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Data.Offset + 1 + Ns * 2);
            return (int)Binary.ReadBits(Data.Array, ref bitOffset, Binary.BitsPerByte, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// End of spectral
    /// </summary>
    public int Se
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Data.Offset + 1 + Ns * 2 + 1);
            return (int)Binary.ReadBits(Data.Array, ref bitOffset, Binary.BitsPerByte, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// Successive approximation bit position high
    /// </summary>
    public int Ah
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Data.Offset + 1 + Ns * 2 + 1 + 1);
            return (int)Binary.ReadBits(Data.Array, ref bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// Successive approximation bit position low or point transform
    /// </summary>
    public int Al
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Data.Offset + 1 + Ns * 2 + 1 + 1) + Binary.Four;
            return (int)Binary.ReadBits(Data.Array, ref bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
    }
}
