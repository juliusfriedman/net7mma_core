using System.Collections.Generic;

namespace Media.Containers.Mpeg
{
    /// <summary>
    /// Describes the various MPEG Stream Types. (ISO13818-2 and compatible)
    /// <see href="http://www.mp4ra.org/object.html">MP4REG</see>
    /// </summary>
    //https://xhelmboyx.tripod.com/formats/mpeg-layout.txt
    public static class StreamTypes
    {
        public const byte Forbidden = 0x00;

        /// <summary>
        /// see 8.5
        /// </summary>
        public const byte ObjectDescriptorStream = 0x01;

        /// <summary>
        /// see 10.2.5
        /// </summary>
        public const byte ClockReferenceStream = 0x02;

        /// <summary>
        /// see 9.2.1
        /// </summary>
        public const byte SceneDescriptionStream = 0x03;

        public const byte VisualStream = 0x04;

        public const byte AudioStream = 0x05;

        public const byte MPEG7Stream = 0x06;

        /// <summary>
        /// see 8.3.2
        /// </summary>
        public const byte IPMPStream = 0x07;

        /// <summary>
        /// see 8.4.2
        /// </summary>
        public const byte ObjectContentInfoStream = 0x08;

        public const byte MPEGJStream = 0x09;

        public const byte InteractionStream = 0x0A;

        public const byte IPMPToolStream = 0x0B;

        public const byte FontDataStream = 0x0C;

        public const byte StreamingText = 0x0D;

        public const byte SequenceHeader = 0xB3;

        public const byte Extension = 0xB5;

        public const byte ProgramEnd = 0xB9;

        public const byte PackHeader = 0xBA;

        public const byte SystemHeader = 0xBB;

        public const byte ProgramStreamMap = 0xBC;

        public const byte PrivateStream1 = 0xBD;

        public const byte PaddingStream = 0xBE;

        public const byte PrivateStream2 = 0xBF;

        public static bool IsMpeg1or2AudioStream(byte code) { return code is >= 0xC0 and <= 0xDF; }

        public static bool IsMpeg1or2VideoStream(byte code) { return code is >= 0xE0 and <= 0xEF; }

        public const byte ECMStream = 0xF0;

        public const byte EMMStream = 0xF1;

        public const byte DMSCCStream = 0xF2;

        public const byte ISO13522Stream = 0xF3;

        public const byte H222TypeA = 0xF4;

        public const byte H222TypeB = 0xF5;

        public const byte H222TypeC = 0xF6;

        public const byte H222TypeD = 0xF7;

        public const byte H222TypeE = 0xF8;

        public const byte AncillaryStream = 0xF9;

        public static bool IsReserverd(byte b) { return b is >= 0xFA and <= 0xFE; }

        public static bool IsUserPrivate(byte b) { return b is >= 0x20 and <= 0x3F; }

        public const byte ProgramStreamDirectory = byte.MaxValue;

        internal static Dictionary<byte, string> StreamTypeMap = [];

        public static string ToTextualConvention(byte b)
        {
            return StreamTypeMap.TryGetValue(b, out string name)
                ? name
                : IsMpeg1or2AudioStream(b)
                ? "Audio"
                : IsMpeg1or2VideoStream(b)
                ? "Video"
                : IsReserverd(b) ? "Reserved" : IsUserPrivate(b) ? "UserPrivate" : Media.Common.Extensions.String.StringExtensions.UnknownString;
        }

        static StreamTypes()
        {
            foreach (var fieldInfo in typeof(StreamTypes).GetFields()) StreamTypeMap.Add((byte)fieldInfo.GetValue(null), fieldInfo.Name);
        }

    }
}
