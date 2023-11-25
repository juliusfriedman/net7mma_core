using System;

namespace Media.Codecs.Audio.Ac3
{
    public class SyncInfo
    {
        public const int SyncWord = 0x0B77;

        //2 = > syncword - 16 bits - 0x0B77
        //2 = > //crc 16 bits
        //1 = > //2 bit, 6 bit 

        public const int Length = 5;

        public static byte GetSampleRateCode(int sampleRate)
        {
            return sampleRate switch
            {
                48000 => 0,
                44100 => 1,
                32000 => 2,
                _ => throw new ArgumentException("Invalid sample rate"),
            };
        }

        /// <returns>Bitrate in kbit/s</returns>
        public static int GetBitRate(byte frameSizeCode)
        {
            return (frameSizeCode / 2) switch
            {
                0 => 32,
                1 => 40,
                2 => 48,
                3 => 56,
                4 => 64,
                5 => 80,
                6 => 96,
                7 => 112,
                8 => 128,
                9 => 160,
                10 => 192,
                11 => 224,
                12 => 256,
                13 => 320,
                14 => 384,
                15 => 448,
                16 => 512,
                17 => 576,
                18 => 640,
                _ => throw new ArgumentException("Invalid frame size code"),
            };
        }

        private static readonly int[,] frameSizeCodeTable = new int[,]
            {
                {96, 69, 64},
                {96, 70, 64},
                {120, 87, 80},
                {120, 88, 80},
                {144, 104, 96},
                {144, 105, 96},
                {168, 121, 112},
                {168, 122, 112},
                {192, 139, 128},
                {192, 140, 128},
                {240, 174, 160},
                {240, 175, 160},
                {288, 208, 192},
                {288, 209, 192},
                {336, 243, 224},
                {336, 244, 224},
                {384, 278, 256},
                {384, 279, 256},
                {480, 348, 320},
                {480, 349, 320},
                {576, 417, 384},
                {576, 418, 384},
                {672, 487, 448},
                {672, 488, 448},
                {768, 557, 512},
                {768, 558, 512},
                {960, 696, 640},
                {960, 697, 640},
                {1152, 835, 768},
                {1152, 836, 768},
                {1344, 975, 896},
                {1344, 976, 896},
                {1536, 1114, 1024},
                {1536, 1115, 1024},
                {1728, 1253, 1152},
                {1728, 1254, 1152},
                {1920, 1393, 1280},
                {1920, 1394, 1280},
            };

        public static int GetFrameSize(byte sampleRateCode, byte frameSizeCode)
        {
            // in 2-byte words:
            if (sampleRateCode == 3)
            {
                throw new NotImplementedException("Unknown sample rate code");
            }
            else if (sampleRateCode > 3)
            {
                throw new ArgumentException("Invalid sample rate code");
            }

            if (frameSizeCode > 37)
            {
                throw new ArgumentException("Invalid frame size code");
            }

            int sampleRateIndex = 2 - sampleRateCode;
            //*2
            return frameSizeCodeTable[frameSizeCode, sampleRateIndex] << 1;
        }
    }
}
