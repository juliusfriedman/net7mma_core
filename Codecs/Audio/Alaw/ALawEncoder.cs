﻿namespace Media.Codecs.Audio.Alaw
{
    /// <summary>
    /// A-law encoder
    /// </summary>
    public sealed class ALawEncoder : Media.Codec.Encoder
    {
        private const int cClip = 32635;
        private static readonly byte[] ALawCompressTable = new byte[128]
        {
             1,1,2,2,3,3,3,3,
             4,4,4,4,4,4,4,4,
             5,5,5,5,5,5,5,5,
             5,5,5,5,5,5,5,5,
             6,6,6,6,6,6,6,6,
             6,6,6,6,6,6,6,6,
             6,6,6,6,6,6,6,6,
             6,6,6,6,6,6,6,6,
             7,7,7,7,7,7,7,7,
             7,7,7,7,7,7,7,7,
             7,7,7,7,7,7,7,7,
             7,7,7,7,7,7,7,7,
             7,7,7,7,7,7,7,7,
             7,7,7,7,7,7,7,7,
             7,7,7,7,7,7,7,7,
             7,7,7,7,7,7,7,7
        };

        /// <summary>
        /// Encodes a single 16 bit sample to a-law
        /// </summary>
        /// <param name="sample">16 bit PCM sample</param>
        /// <returns>a-law encoded byte</returns>
        public static byte LinearToALawSample(short sample)
        {
            int sign;
            int exponent;
            int mantissa;
            byte compressedByte;

            sign = ((~sample) >> 8) & 0x80;
            if (sign is 0)
                sample = (short)-sample;
            if (sample > cClip)
                sample = cClip;
            if (sample >= 256)
            {
                exponent = ALawCompressTable[(sample >> 8) & 0x7F];
                mantissa = (sample >> (exponent + 3)) & 0x0F;
                compressedByte = (byte)((exponent << 4) | mantissa);
            }
            else
            {
                compressedByte = (byte)(sample >> 4);
            }
            compressedByte ^= (byte)(sign ^ 0x55);
            return compressedByte;
        }

        public static byte[] LinearToALaw(short[] pcm)
        {
            byte[] output = new byte[pcm.Length];
            for (int i = 0; i < pcm.Length; i++)
                output[i] = LinearToALawSample(pcm[i]);
            return output;
        }
    }
}
