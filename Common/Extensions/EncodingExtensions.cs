﻿#region Copyright
/*
This file came from Managed Media Aggregation, You can always find the latest version @ https://github.com/juliusfriedman/net7mma_core
  
 Julius.Friedman@gmail.com / (SR. Software Engineer ASTI Transportation Inc. https://www.asti-trans.com)

Permission is hereby granted, free of charge, 
 * to any person obtaining a copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, 
 * including without limitation the rights to :
 * use, 
 * copy, 
 * modify, 
 * merge, 
 * publish, 
 * distribute, 
 * sublicense, 
 * and/or sell copies of the Software, 
 * and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * 
 * 
 * JuliusFriedman@gmail.com should be contacted for further details.

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
 * 
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
 * TORT OR OTHERWISE, 
 * ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 * v//
 */
#endregion

//https://stackoverflow.com/questions/1804433/issue-with-binaryreader-readchars

using System;

namespace Media.Common.Extensions.Encoding
{
    [CLSCompliant(true)]
    public static class EncodingExtensions
    {
        public static readonly char[] EmptyChar = System.Array.Empty<char>();

        #region GetByteCount

        /// <summary>
        /// Allows a call to <see cref="Encoding.GetByteCount"/> with only 1 char, e.g. (GetByteCount('/0'))
        /// Uses the Default encoding if none was provided.
        /// </summary>
        /// <param name="encoding"></param>
        /// <param name="chars"></param>
        /// <returns></returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static int GetByteCount(this System.Text.Encoding encoding, params char[] chars)
        {
            encoding ??= System.Text.Encoding.Default;

            return encoding.GetByteCount(chars);
        }

        #endregion

        #region Number Extraction

        //Todo, See Media.Common.ASCII for an idea of the API required.

        //Candidates for method names:

        //ReadEncodedNumberFrom

        //ReadEncodedNumberWithSignFrom

        #endregion

        #region Read Delimited Data

        public static bool ReadDelimitedDataFrom(this System.Text.Encoding encoding, byte[] buffer, char[] delimits, long offset, long count, out string result, out long read, bool includeDelimits = true)
        {
            int intCount = (int)count, intOffset = (int)offset;

            bool readResult = ReadDelimitedDataFrom(encoding, buffer, delimits, intOffset, intCount, out result, out int intRead, out System.Exception any, includeDelimits);

            read = intRead;

            return readResult;

        }

        public static bool ReadDelimitedDataFrom(this System.Text.Encoding encoding, byte[] buffer, char[] delimits, int offset, int count, out string result, out int read, out System.Exception any, bool includeDelimits = true)
        {
            read = Common.Binary.Zero;

            any = null;

            result = string.Empty;

            //Todo, check for large delemits and use a hash or always use a hash.
            //System.Collections.Generic.HashSet<char> delimitsC = new System.Collections.Generic.HashSet<char>(delimits);

            delimits ??= EmptyChar;


            if (count is Common.Binary.Zero || Common.Extensions.Array.ArrayExtensions.IsNullOrEmpty(buffer, out int max))
            {
                result = null;

                return false;
            }

            //Account for the position
            max -= offset;

            //The smaller of the two, max and count
            if ((count = Common.Binary.Min(ref max, ref count)) is 0) return false;

            bool sawDelimit = false;

            //Make the builder
            System.Text.StringBuilder builder = new();

            //Use default..
            encoding ??= System.Text.Encoding.Default;

            System.Text.Decoder decoder = encoding.GetDecoder();

            //int charCount = decoder.GetCharCount(buffer, offset, count);

            //if(charCount is 0) return true;

            int toRead, delemitsLength = delimits.Length;

            toRead = delemitsLength <= Common.Binary.Zero ? count : Common.Binary.Max(1, delemitsLength);




            //Could use Pool to save allocatios until StringBuilder can handle char*
            char[] results = new char[toRead];

            do
            {
                //Convert to utf16 from the encoding
#if UNSAFE
                unsafe { decoder.Convert((byte*)System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement<byte>(buffer, offset), count, (char*)System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement<char>(results, 0), toRead, count <= 0, out justRead, out charsUsed, out complete); }
#else
                decoder.Convert(buffer, offset, count, results, 0, toRead, count <= 0, out int justRead, out int charsUsed, out bool complete);
#endif

                //If there are not enough bytes to decode the char
                if (justRead is Common.Binary.Zero)
                {
                    break;
                }

                //Move the offsets and count for what was converted from the decoder
                offset += justRead;

                count -= justRead;

                //Iterate the decoded characters looking for a delemit
                if (delemitsLength > Common.Binary.Zero) for (int c = 0, e = charsUsed; c < e; ++c)
                    {
                        //Compare the char decoded to the delimits, if encountered set sawDelimit and either include the delemit or not.
#if NATIVE || UNSAFE
                        if (System.Array.IndexOf<char>(delimits, (char)System.Runtime.InteropServices.Marshal.ReadInt16(System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement<char>(results, c))) >= 0)
#else
                        if (System.Array.IndexOf<char>(delimits, results[c]) >= 0)
#endif
                        {
                            sawDelimit = true;

                            charsUsed = includeDelimits is false ? c : ++c;

                            break;
                        }
                    }

                if (charsUsed > 0) builder.Append(results, 0, charsUsed);
            } while (count > 0 && sawDelimit is false);

            if (builder is null)
            {
                result = null;

                return sawDelimit;
            }

            result = builder.Length is Common.Binary.Zero ? string.Empty : builder.ToString();

            //Take the amount of bytes in the string as what was read.
            read = encoding.GetByteCount(result);

            builder = null;

            return sawDelimit;
        }

        /// <summary>
        /// Reads the data in the stream using the given encoding until the first occurance of any of the given delimits are found, count is reached or the end of stream occurs.
        /// </summary>
        /// <param name="encoding"></param>
        /// <param name="stream"></param>
        /// <param name="delimits"></param>
        /// <param name="count"></param>
        /// <param name="result"></param>
        /// <param name="read"></param>
        /// <param name="any"></param>
        /// <param name="includeDelimits"></param>
        /// <returns></returns>
        [CLSCompliant(false)]
        public static bool ReadDelimitedDataFrom(this System.Text.Encoding encoding, System.IO.Stream stream, char[] delimits, ulong count, out string result, out ulong read, out System.Exception any, bool includeDelimits = true)
        {
            read = Common.Binary.Zero;

            any = null;

            result = string.Empty;

            //Todo, check for large delemits and use a hash or always use a hash.
            //System.Collections.Generic.HashSet<char> delimitsC = new System.Collections.Generic.HashSet<char>(delimits);

            delimits ??= EmptyChar;

            if (stream is null || stream.CanRead is false || count is Common.Binary.Zero)
            {
                result = null;

                return false;
            }

            long at = stream.Position;// max = stream.Length

            //Let the exception enfore the bounds for now
            if (at >= stream.Length) return false;

            //Use default..
            encoding ??= System.Text.Encoding.Default;

            System.Text.StringBuilder builder = null;

            bool sawDelimit = false;

            //Make the builder
            builder = new System.Text.StringBuilder();

            //Use the BinaryReader on the stream to ensure ReadChar reads in the correct size
            //This prevents manual conversion from byte to char and uses the encoding's code page.
            using (var br = new System.IO.BinaryReader(stream, encoding, true))
            {
                char cached;

                //Read a while permitted, check for EOS
                while (read < count && stream.CanRead)
                {
                    try
                    {
                        //Get the char in the encoding beging used
                        cached = br.ReadChar();

                        //Determine where ReadChar advanced to (e.g. if Fallback occured)
                        read = (ulong)(stream.Position - at);

                        //delimitsC.Contains(cached);

                        //If the char was a delemit, indicate the delimit was seen
                        if (sawDelimit = System.Array.IndexOf<char>(delimits, cached) >= 0)
                        {
                            //if the delemit should be included, include it.
                            if (includeDelimits) builder.Append(cached);

                            //Do not read further
                            goto Done;
                        }

                        //append the char
                        builder.Append(cached);
                    }
                    catch (System.Exception ex)
                    {
                        //Handle the exception
                        any = ex;

                        //Take note of the position only during exceptions
                        read = (ulong)(stream.Position - at);

                        //Do not read further
                        goto Done;
                    }
                }
            }

            Done:

            if (builder is null)
            {
                result = null;

                return sawDelimit;
            }

            result = builder.Length is 0 ? string.Empty : builder.ToString();

            builder = null;

            return sawDelimit;
        }

        /// <summary>
        /// Reads the data in the stream using the given encoding until the first occurance of any of the given delimits are found, count is reached or the end of stream occurs.
        /// </summary>
        /// <param name="encoding"></param>
        /// <param name="stream"></param>
        /// <param name="delimits"></param>
        /// <param name="count"></param>
        /// <param name="result"></param>
        /// <param name="includeDelimits"></param>
        /// <returns>True if a given delimit was found, otherwise false.</returns>        
        [CLSCompliant(false)]
        public static bool ReadDelimitedDataFrom(this System.Text.Encoding encoding, System.IO.Stream stream, char[] delimits, ulong count, out string result, out ulong read, bool includeDelimits = true)
        {

            return ReadDelimitedDataFrom(encoding, stream, delimits, count, out result, out read, out System.Exception encountered, includeDelimits);
        }

        public static bool ReadDelimitedDataFrom(this System.Text.Encoding encoding, System.IO.Stream stream, char[] delimits, long count, out string result, out long read, out System.Exception any, bool includeDelimits = true)
        {

            bool found = ReadDelimitedDataFrom(encoding, stream, delimits, (ulong)count, out result, out ulong cast, out any, includeDelimits);

            read = (int)cast;

            return found;
        }

        public static bool ReadDelimitedDataFrom(this System.Text.Encoding encoding, System.IO.Stream stream, char[] delimits, int count, out string result, out int read, bool includeDelimits = true)
        {

            bool found = ReadDelimitedDataFrom(encoding, stream, delimits, (ulong)count, out result, out ulong cast, includeDelimits);

            read = (int)cast;

            return found;
        }

        public static bool ReadDelimitedDataFrom(this System.Text.Encoding encoding, System.IO.Stream stream, char[] delimits, long count, out string result, out long read, bool includeDelimits = true)
        {

            bool found = ReadDelimitedDataFrom(encoding, stream, delimits, (ulong)count, out result, out ulong cast, includeDelimits);

            read = (long)cast;

            return found;
        }

        #endregion

        #region GetChars

        /// <summary>
        /// Encodes the given bytes as <see cref="char"/>'s using the specified options.
        /// </summary>
        /// <param name="encoding">The optional encoding to use, if none is specified the Default will be used.</param>
        /// <param name="toEncode">The data to encode, if null an <see cref="ArgumentNullException"/> will be thrown.</param>
        /// <returns>The encoded data.</returns>
        public static char[] GetChars(this System.Text.Encoding encoding, params byte[] toEncode)
        {
            if (toEncode is null) throw new ArgumentNullException("toEncode");

            //int firstDimension = toEncode.Rank -1;

            //get the length
            int toEncodeLength = toEncode.GetUpperBound(0);

            //If 0 then return the empty char array
            if (toEncodeLength is 0) return EmptyChar;

            //GetChars using the first element and the length
            return GetChars(encoding, toEncode, toEncode.GetLowerBound(0), toEncodeLength);

        }

        /// <summary>
        /// Encodes the given bytes as <see cref="char"/>'s using the specified options using <see cref="System.Text.Encoding.GetChars"/>.
        /// </summary>
        /// <param name="encoding">The optional encoding to use, if none is specified the Default will be used.</param>
        /// <param name="toEncode">The data to encode, if null an <see cref="ArgumentNullException"/> will be thrown.</param>
        /// <param name="offset">The offset to start at</param>
        /// <param name="count">The amount of bytes to use in the encoding</param>
        /// <returns>The encoded data</returns>
        public static char[] GetChars(this System.Text.Encoding encoding, byte[] toEncode, int offset, int count)
        {
            //Use default..
            encoding ??= System.Text.Encoding.Default;

            return encoding.GetChars(toEncode, offset, count);
        }

        /// <summary>
        /// Encodes the given bytes as <see cref="char"/>'s using the specified options.
        /// </summary>
        /// <param name="encoding">The optional encoding to use, if none is specified the Default will be used.</param>
        /// <param name="toEncode">The data to encode, if null an <see cref="ArgumentNullException"/> will be thrown.</param>
        /// <returns>The encoded data.</returns>
        public static char[] GetChars(this System.Text.Decoder decoder, params byte[] toEncode)
        {
            if (toEncode is null) throw new ArgumentNullException("toEncode");

            //int firstDimension = toEncode.Rank -1;

            //get the length
            int toEncodeLength = toEncode.GetUpperBound(0);

            //If 0 then return the empty char array
            if (toEncodeLength is 0) return EmptyChar;

            //GetChars using the first element and the length
            return GetChars(decoder, toEncode, toEncode.GetLowerBound(0), toEncodeLength);

        }

        /// <summary>
        /// Encodes the given bytes as <see cref="char"/>'s using the specified options using <see cref="System.Text.Encoding.GetChars"/>.
        /// </summary>
        /// <param name="encoding">The optional encoding to use, if none is specified the Default will be used.</param>
        /// <param name="toEncode">The data to encode, if null an <see cref="ArgumentNullException"/> will be thrown.</param>
        /// <param name="offset">The offset to start at</param>
        /// <param name="count">The amount of bytes to use in the encoding</param>
        /// <returns>The encoded data</returns>
        public static char[] GetChars(this System.Text.Decoder decoder, byte[] toEncode, int offset, int count)
        {
            //Use default..
            decoder ??= System.Text.Encoding.Default.GetDecoder();

            return decoder.GetChars(toEncode, offset, count);
        }

        #endregion

        #region AllocFreeAppend

        ////https://github.com/dotnet/corefx/issues/2102
        //public static unsafe System.Text.StringBuilder AllocFreeAppend(this System.Text.Encoding encoding, byte* bytes, int byteCount, System.Text.StringBuilder builder)
        //{
        //    int charCount = encoding.GetCharCount(bytes, byteCount);

        //    char* chars = stackalloc char[charCount];

        //    int charsWritten = encoding.GetChars(bytes, byteCount, chars, charCount);

        //    builder.Append(chars, charsWritten);

        //    return builder;
        //}

        #endregion
    }
}


namespace Media.UnitTests
{
    internal class EncodingExtensionsTests
    {

        /// <summary>
        /// Performs a test that `ReadDelimitedDataFrom` can read the same data back as was written in various different encodings.
        /// </summary>
        public void TestReadDelimitedDataFrom()
        {
            //Unicode string
            string testString = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz01234567890!@#$%^&*()_+-=";

            int testStringLength = testString.Length;

            //With every encoding in the system
            foreach (var encodingInfo in System.Text.Encoding.GetEncodings())
            {
                //Create a new memory stream
                using (var ms = new System.IO.MemoryStream())
                {
                    //Get the encoding
                    var encoding = encodingInfo.GetEncoding();

                    System.Console.WriteLine("Testing: " + encoding.EncodingName);

                    //Create a writer on that same stream using a small buffer
                    using (var streamWriter = new System.IO.StreamWriter(ms, encoding, 1, true))
                    {
                        //Get the binary representation of the string in the encoding being tested
                        var encodedData = encoding.GetBytes(testString);

                        //Cache the length of the data
                        int encodedDataLength = encodedData.Length;

                        //Write the value in the encoding
                        streamWriter.Write(testString);

                        //Ensure in the stream
                        streamWriter.Flush();

                        //Go back to the beginning
                        ms.Position = 0;



                        //Ensure that was read correctly using the binary length and not the string length
                        //(should try to over read)
                        if (false != Media.Common.Extensions.Encoding.EncodingExtensions.ReadDelimitedDataFrom(encoding, ms, null, encodedDataLength, out string actual, out int read))
                        {
                            throw new System.Exception("ReadDelimitedDataFrom failed.");
                        }

                        //Ensure the position 
                        if (ms.Position > encodedDataLength + encoding.GetPreamble().Length)
                        {
                            throw new System.Exception("Stream.Position is not correct.");
                        }

                        //Ensure the strings are equal (The extra byte is spacing)
                        int difference = string.Compare(encoding.GetString(encoding.GetBytes(testString)), actual);
                        if (difference is not 0 and > 1)
                        {
                            throw new System.Exception("string data is incorrect.");
                        }

                        Console.WriteLine(actual);
                    }
                }

            }

        }

    }
}