namespace Media.Codecs.Audio.Mulaw
{
    /// <summary>
    /// mu-law encoder
    /// based on code from:
    /// http://hazelware.luggle.com/tutorials/mulawcompression.html
    /// https://www.codeproject.com/Articles/14237/Using-the-G711-standard
    /// </summary>
    public sealed class MuLawEncoder : Media.Codec.Encoder
    {
        public const int BIAS = 0x84; //aka 132, or 1000 0100
        public const int MAX = 32635; //32767 (max 15-bit integer) minus BIAS

        /// <summary>
        /// Sets whether or not all-zero bytes are encoded as 2 instead.
        /// The pcm values this concerns are in the range [32768,33924] (unsigned).
        /// </summary>
        public bool ZeroTrap
        {
            get { return pcmToMuLawMap[33000] != 0; }
            set
            {
                byte val = (byte)(value ? 2 : 0);
                for (int i = 32768; i <= 33924; i++)
                    pcmToMuLawMap[i] = val;
            }
        }

        /// <summary>
        /// An array where the index is the 16-bit PCM input, and the value is
        /// the mu-law result.
        /// </summary>
        private static byte[] pcmToMuLawMap;

        static MuLawEncoder()
        {
            pcmToMuLawMap = new byte[65536];
            for (int i = short.MinValue; i <= short.MaxValue; i++)
                pcmToMuLawMap[i & 0xffff] = encode(i);
        }

        /// <summary>
        /// Encode one mu-law byte from a 16-bit signed integer. Internal use only.
        /// </summary>
        /// <param name="pcm">A 16-bit signed pcm value</param>
        /// <returns>A mu-law encoded byte</returns>
        private static byte encode(int pcm)
        {
            //Get the sign bit.  Shift it for later use without further modification
            int sign = (pcm & 0x8000) >> 8;
            //If the number is negative, make it positive (now it's a magnitude)
            if (sign != 0)
                pcm = -pcm;
            //The magnitude must be less than 32635 to avoid overflow
            if (pcm > MAX) pcm = MAX;
            //Add 132 to guarantee a 1 in the eight bits after the sign bit
            pcm += BIAS;

            /* Finding the "exponent"
             * Bits:
             * 1 2 3 4 5 6 7 8 9 A B C D E F G
             * S 7 6 5 4 3 2 1 0 . . . . . . .
             * We want to find where the first 1 after the sign bit is.
             * We take the corresponding value from the second row as the exponent value.
             * (i.e. if first 1 at position 7 -> exponent = 2) */
            int exponent = 7;
            //Move to the right and decrement exponent until we hit the 1
            for (int expMask = 0x4000; (pcm & expMask) is 0; exponent--, expMask >>= 1) { }

            /* The last part - the "mantissa"
             * We need to take the four bits after the 1 we just found.
             * To get it, we shift 0x0f :
             * 1 2 3 4 5 6 7 8 9 A B C D E F G
             * S 0 0 0 0 0 1 . . . . . . . . . (meaning exponent is 2)
             * . . . . . . . . . . . . 1 1 1 1
             * We shift it 5 times for an exponent of two, meaning
             * we will shift our four bits (exponent + 3) bits.
             * For convenience, we will actually just shift the number, then and with 0x0f. */
            int mantissa = (pcm >> (exponent + 3)) & 0x0f;

            //The mu-law byte bit arrangement is SEEEMMMM (Sign, Exponent, and Mantissa.)
            byte mulaw = (byte)(sign | exponent << 4 | mantissa);

            //Last is to flip the bits
            return (byte)~mulaw;
        }

        /// <summary>
        /// Encode a pcm value into a mu-law byte
        /// </summary>
        /// <param name="pcm">A 16-bit pcm value</param>
        /// <returns>A mu-law encoded byte</returns>
        public static byte MuLawEncode(float pcm) => MuLawEncode((int)pcm);

        /// <summary>
        /// Encode a pcm value into a mu-law byte
        /// </summary>
        /// <param name="pcm">A 16-bit pcm value</param>
        /// <returns>A mu-law encoded byte</returns>
        public static byte MuLawEncode(int pcm)
        {
            return pcmToMuLawMap[pcm & 0xffff];
        }

        /// <summary>
        /// Encode a pcm value into a mu-law byte
        /// </summary>
        /// <param name="pcm">A 16-bit pcm value</param>
        /// <returns>A mu-law encoded byte</returns>
        public static byte MuLawEncode(short pcm)
        {
            return pcmToMuLawMap[pcm & 0xffff];
        }

        /// <summary>
        /// Encode an array of pcm values
        /// </summary>
        /// <param name="data">An array of 16-bit pcm values</param>
        /// <returns>An array of mu-law bytes containing the results</returns>
        public static byte[] MuLawEncode(int[] data)
        {
            int size = data.Length;
            byte[] encoded = new byte[size];
            for (int i = 0; i < size; i++)
                encoded[i] = MuLawEncode(data[i]);
            return encoded;
        }

        /// <summary>
        /// Encode an array of pcm values
        /// </summary>
        /// <param name="data">An array of 16-bit pcm values</param>
        /// <returns>An array of mu-law bytes containing the results</returns>
        public static byte[] MuLawEncode(float[] data)
        {
            int size = data.Length;
            byte[] encoded = new byte[size];
            for (int i = 0; i < size; i++)
                encoded[i] = MuLawEncode(data[i]);
            return encoded;
        }

        /// <summary>
        /// Encode an array of pcm values
        /// </summary>
        /// <param name="data">An array of 16-bit pcm values</param>
        /// <returns>An array of mu-law bytes containing the results</returns>
        public static byte[] MuLawEncode(short[] data)
        {
            int size = data.Length;
            byte[] encoded = new byte[size];
            for (int i = 0; i < size; i++)
                encoded[i] = MuLawEncode(data[i]);
            return encoded;
        }

        /// <summary>
        /// Encode an array of pcm values
        /// </summary>
        /// <param name="data">An array of bytes in Little-Endian format</param>
        /// <returns>An array of mu-law bytes containing the results</returns>
        public static byte[] MuLawEncode(byte[] data)
        {
            int size = data.Length / 2;
            byte[] encoded = new byte[size];
            for (int i = 0; i < size; i++)
                encoded[i] = MuLawEncode((data[2 * i + 1] << 8) | data[2 * i]);
            return encoded;
        }

        /// <summary>
        /// Encode an array of pcm values into a pre-allocated target array
        /// </summary>
        /// <param name="data">An array of bytes in Little-Endian format</param>
        /// <param name="target">A pre-allocated array to receive the mu-law bytes.  This array must be at least half the size of the source.</param>
        public static void MuLawEncode(byte[] data, byte[] target)
        {
            int size = data.Length / 2;
            for (int i = 0; i < size; i++)
                target[i] = MuLawEncode((data[2 * i + 1] << 8) | data[2 * i]);
        }
    }
}
