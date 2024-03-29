﻿//Should be in VideoElementaryStream or MPEG

namespace Media.Containers.Mpeg
{
    /// <summary>
    /// Contains common values used by the Motion Picture Experts Group.
    /// </summary>
    public static class StartCodes
    {
        /// <summary>
        /// Syncwords are determined using 4 bytes, the first 3 of which are always 0x000001
        /// </summary>
        public static byte[] StartCodePrefix = new byte[] { 0x00, 0x00, 0x01 };

        public static bool IsReserved(byte b)
        {
            return b switch
            {
                0xB0 or 0xB1 or 0xB6 => true,
                _ => false,
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool IsSystemStartCode(byte b) { return b >= 0xBA; }

        //0 - 31
        public const byte Picture = 0x00;

        //Probably Key Frames.
        public static bool IsVideoObjectStartCode(byte b) { return b is >= Picture and <= Common.Binary.FiveBitMaxValue; }

        //0x20 probably a key frame.
        public static bool IsVisalObjectLayerStartCode(byte code) { return code is >= 0x20 and <= 0x2f; }

        public static bool IsSliceStartCode(byte code) { return code is >= 0x01 and <= 0xAF; }

        //MPEG 4 Specific Name (Reversed in MPEG 1 and 2)
        //Probably key frame
        //public const byte VisalObjectSequence = 0xB0;

        //MPEG 4 Specific Name (Reserved in MPEG 1 and 2)
        //public const byte End = 0xB1;

        public const byte UserData = 0xB2;

        public const byte SequenceHeader = 0xB3;

        public const byte SequenceError = 0xB4;

        public const byte Extension = 0xB5;

        //VideoObjectLayer is a Mpeg 4 Extension to Mpeg 2 = 0xB5

        //Key frame if next 2 bits are 0
        public const byte VideoObjectPlane = 0xB6;

        public const byte SequenceEnd = 0xB7;

        //VideoObjectLayerEnd is a Mpeg 4 Extension to Mpeg 2 = 0xB7

        //Of Picture, Video Object Plane, etc.
        public const byte Group = 0xB8;

        /// StreamTypes.ProgramEnd...
        public const byte EndCode = 0xB9;
    }
}
