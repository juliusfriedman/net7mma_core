namespace Media.Codecs.Audio.Mulaw
{
    /// <summary>
    /// mu-law decoder
    /// based on code from:
    /// http://hazelware.luggle.com/tutorials/mulawcompression.html
    /// </summary>
    public sealed class MuLawDecoder : Media.Codec.Decoder
    {
        /// <summary>
        /// An array where the index is the mu-law input, and the value is
        /// the 16-bit PCM result.
        /// </summary>
        private static short[] muLawToPcmMap;

        static MuLawDecoder()
        {
            muLawToPcmMap = new short[256];
            for (byte i = 0; i < byte.MaxValue; i++)
                muLawToPcmMap[i] = decode(i);
        }

        /// <summary>
        /// Decode one mu-law byte. For internal use only.
        /// </summary>
        /// <param name="mulaw">The encoded mu-law byte</param>
        /// <returns>A short containing the 16-bit result</returns>
        private static short decode(byte mulaw)
        {
            //Flip all the bits
            mulaw = (byte)~mulaw;

            //Pull out the value of the sign bit
            int sign = mulaw & 0x80;
            //Pull out and shift over the value of the exponent
            int exponent = (mulaw & 0x70) >> 4;
            //Pull out the four bits of data
            int data = mulaw & 0x0f;

            //Add on the implicit fifth bit (we know the four data bits followed a one bit)
            data |= 0x10;
            /* Add a 1 to the end of the data by shifting over and adding one.  Why?
             * Mu-law is not a one-to-one function.  There is a range of values that all
             * map to the same mu-law byte.  Adding a one to the end essentially adds a
             * "half byte", which means that the decoding will return the value in the
             * middle of that range.  Otherwise, the mu-law decoding would always be
             * less than the original data. */
            data <<= 1;
            data += 1;
            /* Shift the five bits to where they need to be: left (exponent + 2) places
             * Why (exponent + 2) ?
             * 1 2 3 4 5 6 7 8 9 A B C D E F G
             * . 7 6 5 4 3 2 1 0 . . . . . . . <-- starting bit (based on exponent)
             * . . . . . . . . . . 1 x x x x 1 <-- our data
             * We need to move the one under the value of the exponent,
             * which means it must move (exponent + 2) times
             */
            data <<= exponent + 2;
            //Remember, we added to the original, so we need to subtract from the final
            data -= MuLawEncoder.BIAS;
            //If the sign bit is 0, the number is positive. Otherwise, negative.
            return (short)(sign == 0 ? data : -data);
        }

        /// <summary>
        /// Decode one mu-law byte
        /// </summary>
        /// <param name="mulaw">The encoded mu-law byte</param>
        /// <returns>A short containing the 16-bit result</returns>
        public static short MuLawDecode(byte mulaw)
        {
            return muLawToPcmMap[mulaw];
        }

        /// <summary>
        /// Decode an array of mu-law encoded bytes
        /// </summary>
        /// <param name="data">An array of mu-law encoded bytes</param>
        /// <returns>An array of shorts containing the results</returns>
        public static short[] MuLawDecode(byte[] data)
        {
            int size = data.Length;
            short[] decoded = new short[size];
            for (int i = 0; i < size; i++)
                decoded[i] = muLawToPcmMap[data[i]];
            return decoded;
        }

        /// <summary>
        /// Decode an array of mu-law encoded bytes
        /// </summary>
        /// <param name="data">An array of mu-law encoded bytes</param>
        /// <param name="decoded">An array of shorts containing the results</param>
        /// <remarks>Same as the other method that returns an array of shorts</remarks>
        public static void MuLawDecode(byte[] data, out short[] decoded)
        {
            int size = data.Length;
            decoded = new short[size];
            for (int i = 0; i < size; i++)
                decoded[i] = muLawToPcmMap[data[i]];
        }

        /// <summary>
        /// Decode an array of mu-law encoded bytes
        /// </summary>
        /// <param name="data">An array of mu-law encoded bytes</param>
        /// <param name="decoded">An array of bytes in Little-Endian format containing the results</param>
        public static void MuLawDecode(byte[] data, out byte[] decoded)
        {
            int size = data.Length;
            decoded = new byte[size * 2];
            for (int i = 0; i < size; i++)
            {
                //First byte is the less significant byte
                decoded[2 * i] = (byte)(muLawToPcmMap[data[i]] & 0xff);
                //Second byte is the more significant byte
                decoded[2 * i + 1] = (byte)(muLawToPcmMap[data[i]] >> 8);
            }
        }
    }
}
