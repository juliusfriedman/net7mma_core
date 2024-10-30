using Media.Codec.Jpeg;
using Media.Common;

namespace Codec.Jpeg.Segments;

internal class RestartInterval : Marker
{
    public new const int Length = 2;

    public RestartInterval(int restartInterval)
    : base(Markers.DataRestartInterval, LengthBytes + Length)
    {
        Ri = restartInterval;
    }

    /// <summary>
    /// Restart interval – Specifies the number of MCU in the restart interval.
    /// </summary>
    public int Ri
    {
        get => Binary.Read16(Array, DataOffset + 2, Binary.IsLittleEndian);
        set => Binary.Write16(Array, DataOffset + 2, Binary.IsLittleEndian, (ushort)value);
    }
}
