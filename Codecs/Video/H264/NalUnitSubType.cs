using System;

namespace Media.Codecs.Video.H264
{
    public static class NalUnitSubType
    {
        public const byte Reserved = 0x00;

        public const byte SingleNalUnitPacket = 0x01;

        public const byte AggregationPacket = 0x02;

        [CLSCompliant(false)]
        public static bool IsReserved(ref byte subType) { return subType == NalUnitSubType.Reserved || subType >= 3 && subType <= Common.Binary.FiveBitMaxValue; }

        [CLSCompliant(true)]
        public static bool IsReserved(byte subType) { return IsReserved(ref subType); }
    }
}
