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

namespace Media.Common.Extensions.String
{
    public static class StringExtensions
    {
        public const string UnknownString = "Unknown";

        //Standard Numeric Format Strings
        //https://msdn.microsoft.com/en-us/library/dwhawy9k(v=vs.110).aspx

        //Custom Numeric Format Strings
        //https://msdn.microsoft.com/en-us/library/0c899ak8(v=vs.110).aspx

        public const string HexadecimalFormat = "X";

        #region Hex Functions

        //https://github.com/dotnet/corefx/issues/10013

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static byte HexCharToByte(char c, bool upperCase = false)
        {
            c = char.ToUpperInvariant(c); return (byte)(upperCase ? char.ToUpperInvariant((char)(c > '9' ? c - 'A' + 10 : c - '0')) : (c > '9' ? c - 'A' + 10 : c - '0'));
        }

        /// <summary>
        /// Converts a String in the form 0011AABB to a Byte[] using the chars in the string as bytes to caulcate the decimal value.
        /// </summary>
        /// <notes>
        /// Reduced string allocations from managed version substring
        /// About 10 milliseconds faster then Managed when doing it 100,000 times. otherwise no change
        /// </notes>
        /// <param name="str"></param>
        /// <returns></returns>
        public static byte[] HexStringToBytes(string str, int start = 0, int length = -1, bool onlyLettersOrDigits = true)
        {
            //Dont check the results for overflow
            unchecked
            {
                if (length is Common.Binary.Zero) return null;

                if (length <= -1) length = str.Length;

                int check = length - start;

                if (start > check) throw new System.ArgumentOutOfRangeException(nameof(start));

                if (length > check) throw new System.ArgumentOutOfRangeException(nameof(length));

                System.Collections.Generic.IEnumerable<byte> result = Media.Common.MemorySegment.EmptyBytes;

                //Iterate the pointer using the managed length ....
                //Todo, optomize with reverse or i - 1
                for (int i = start, e = length; i < e; i += 2)
                {
                    //to reduce string manipulations pre call
                    if (onlyLettersOrDigits && char.IsLetterOrDigit(str[i]) is false)
                    {
                        //Back up 1
                        --i;

                        //Increase by 2
                        continue;
                    }

                    //Todo, Native and Unsafe

                    //Convert 2 Chars to a byte
                    result = System.Linq.Enumerable.Concat(result, Media.Common.Extensions.Linq.LinqExtensions.Yield((byte)(HexCharToByte(str[i]) << 4 | HexCharToByte(str[i + 1]))));
                }

                //Dont use a List..

                //Return the bytes
                return System.Linq.Enumerable.ToArray(result);
            }
        }

        #endregion

        //https://stackoverflow.com/questions/272633/add-spaces-before-capital-letters
        //Before I ever saw the above I came up with the below... I did modify one thing after
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static string AddSpacesBeforeCapitols(string value, int offset = 0, int count = -1)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;

            System.Text.StringBuilder sb;

            //Take the cast of the byte to char as a constant
            const char SPACE = (char)Common.ASCII.Space;

            try
            {
                count = count < 0 ? value.Length : count;

                //Start with the value and the same capacity as there may be no capitols..
                sb = new System.Text.StringBuilder(value, offset, count, count);

                for (int i = offset; i < count; ++i) //Move 1 character each iteration
                {
                    //If whitespace count as insert
                    if (char.IsWhiteSpace(sb[i])) offset = i;
                    //only for upper characters AND when the previous insert was more than 1 character away
                    else if (char.IsUpper(sb[i]) && i - offset > 1)
                    {
                        //Insert 1 character into the builder @ i, Move 1 character
                        sb.Insert(offset = i++, SPACE);

                        //Increase the loop bound by 1 character
                        ++count;
                    }
                }

                //The string instance now has spaces after each capitol letter.
                return sb.ToString();
            }
            catch
            {
                //Out of memory or otherwise...
                throw;
            }
            finally
            {
                sb = null;
            }
        }

        //Capitolize(string what)

        //Todo, 
        //IsPalindrome(bool caseInsenitive, bool onlyLettersOrDigits)

        //IsNonCharacterPalindrome() => only non letters or digits are checked

        /// <summary>
        /// See <see cref="Media.Common.Extensions.String.StringExtensions.HexStringToBytes"/>
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static byte[] ConvertToBytes(this string hex) { return string.IsNullOrWhiteSpace(hex) ? Media.Common.MemorySegment.EmptyBytes : HexStringToBytes(hex); }

        public static string Substring(this string source, string pattern, System.StringComparison comparison = System.StringComparison.OrdinalIgnoreCase)
        {
            return Substring(source, 0, -1, pattern, comparison);
        }

        /// <summary>
        /// Given a source string find the pattern and extract the data thereafter.
        /// </summary>
        /// <param name="source">The source <see cref="string"/></param>
        /// <param name="startIndex">in <paramref name="source"/></param>
        /// <param name="count">from <paramref name="startIndex"/>, ensured to result in a value outside of the length of <paramref name="source"/></param>
        /// <param name="pattern">The <see cref="string"/> to find in <paramref name="source"/></param>
        /// <param name="comparison"><see cref="System.StringComparison"/></param>
        /// <returns>
        /// <see cref="String.Empty"/> if no result was found or <paramref name="source"/> was null or empty. 
        /// When <paramref name="pattern"/> is null or empty <paramref name="source"/> is returned. 
        /// Otherwise the <see cref="string"/> which does not include the <paramref name="pattern"/>
        /// </returns>
        /// <remarks>8 bytes in worst case space complexity, time complexity is O(count) in worst cast</remarks>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        //refs
        public static string Substring(this string source, int startIndex, int count, string pattern, System.StringComparison comparison = System.StringComparison.OrdinalIgnoreCase) //Ordinal is faster.
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;
            else if (string.IsNullOrEmpty(pattern)) return source;

            int sourceLength = source.Length;

            //Ensure the start is within the string.
            if (startIndex > sourceLength) startIndex -= sourceLength;

            int patternLength = pattern.Length;

            //Use the source length when count is negitive
            if (count < Common.Binary.Zero) count = sourceLength;

            //Only match up to the length of the source.
            count = Binary.Max(ref count, ref sourceLength);

            //Ensure the startIndex and count are within range.
            if (startIndex + count > sourceLength) count -= startIndex;

            //Determine where in the source string the substring resides
            startIndex = source.IndexOf(pattern, startIndex, count, comparison);

            //The substring must be within the source after the length of the pattern.
            return startIndex >= Common.Binary.Zero && startIndex <= sourceLength ? source.Substring(startIndex + patternLength) : string.Empty;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static string[] SplitTrim(this string ex, string[] seperator, int count, System.StringSplitOptions options)
        {
            if (count is Common.Binary.Zero || Common.Extensions.Array.ArrayExtensions.IsNullOrEmpty(seperator)) return new string[Common.Binary.Zero];

            string[] results = ex.Split(seperator, count, options);

            for (int i = results.Length - 1; i >= Common.Binary.Zero; --i) results[i] = results[i].Trim();

            return results;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static string[] SplitTrim(this string ex, char[] seperator, int count, System.StringSplitOptions options)
        {
            if (count is Common.Binary.Zero || Common.Extensions.Array.ArrayExtensions.IsNullOrEmpty(seperator)) return new string[0];

            string[] results = ex.Split(seperator, count, options);

            for (int i = results.Length - 1; i >= Common.Binary.Zero; --i) results[i] = results[i].Trim();

            return results;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static string[] SplitTrimEnd(this string ex, string[] seperator, int count, System.StringSplitOptions options)
        {
            if (count is Common.Binary.Zero || Common.Extensions.Array.ArrayExtensions.IsNullOrEmpty(seperator)) return new string[Common.Binary.Zero];

            string[] results = ex.Split(seperator, count, options);

            for (int i = results.Length - 1; i >= Common.Binary.Zero; --i) results[i] = results[i].TrimEnd();

            return results;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static string[] SplitTrimEnd(this string ex, char[] seperator, int count, System.StringSplitOptions options)
        {
            if (count is Common.Binary.Zero || Common.Extensions.Array.ArrayExtensions.IsNullOrEmpty(seperator)) return new string[Common.Binary.Zero];

            string[] results = ex.Split(seperator, count, options);

            for (int i = results.Length - 1; i >= Common.Binary.Zero; --i) results[i] = results[i].TrimEnd();

            return results;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static string[] SplitTrimStart(this string ex, string[] seperator, int count, System.StringSplitOptions options)
        {
            if (count is Common.Binary.Zero || Common.Extensions.Array.ArrayExtensions.IsNullOrEmpty(seperator)) return new string[Common.Binary.Zero];

            string[] results = ex.Split(seperator, count, options);

            for (int i = results.Length - 1; i >= Common.Binary.Zero; --i) results[i] = results[i].TrimStart();

            return results;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static string[] SplitTrimStart(this string ex, char[] seperator, int count, System.StringSplitOptions options)
        {
            if (count is Common.Binary.Zero || Common.Extensions.Array.ArrayExtensions.IsNullOrEmpty(seperator)) return new string[Common.Binary.Zero];

            string[] results = ex.Split(seperator, count, options);

            for (int i = results.Length - 1; i >= Common.Binary.Zero; --i) results[i] = results[i].TrimStart();

            return results;
        }

        /// <summary>
        /// Splits a string into substrings that are based on the characters in an array. 
        /// </summary>
        /// <param name="value">The string to split.</param>
        /// <param name="options"><see cref="StringSplitOptions.RemoveEmptyEntries"/> to omit empty array elements from the array returned; or <see cref="StringSplitOptions.None"/> to include empty array elements in the array returned.</param>
        /// <param name="count">The maximum number of substrings to return.</param>
        /// <param name="separator">A character array that delimits the substrings in this string, an empty array that contains no delimiters, or null. </param>
        /// <returns></returns>
        /// <remarks>
        /// Delimiter characters are not included in the elements of the returned array. 
        /// If this instance does not contain any of the characters in separator the returned sequence consists of a single element that contains this instance.
        /// If the separator parameter is null or contains no characters, white-space characters are assumed to be the delimiters. White-space characters are defined by the Unicode standard and return true if they are passed to the <see cref="Char.IsWhiteSpace"/> method.
        /// </remarks>
        public static System.Collections.Generic.IEnumerable<string> SplitLazy(this string value, int count = int.MaxValue, System.StringSplitOptions options = System.StringSplitOptions.None, params char[] separator)
        {
            if (count <= 0)
            {
                if (count < 0) throw new System.ArgumentOutOfRangeException(nameof(count), "Count cannot be less than zero.");
                yield break;
            }

            if (string.IsNullOrEmpty(value) || count is 1 || value.IndexOfAny(separator) is not -1)
            {
                yield return value;
                yield break;
            }

            System.Func<char, bool> predicate = separator is not null && separator.Length != 0
                ? ((c) => Common.Extensions.Array.ArrayExtensions.Contains(separator, c))
                : char.IsWhiteSpace;
            bool removeEmptyEntries = (options & System.StringSplitOptions.RemoveEmptyEntries) != 0;
            int ct = 0;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < value.Length; ++i)
            {
                char c = value[i];
                if (predicate(c) is false)
                {
                    sb.Append(c);
                }
                else
                {
                    if (sb.Length != 0)
                    {
                        yield return sb.ToString();
                        sb.Clear();
                    }
                    else
                    {
                        if (removeEmptyEntries)
                            continue;
                        yield return string.Empty;
                    }

                    if (++ct >= count - 1)
                    {
                        if (removeEmptyEntries)
                            while (++i < value.Length && predicate(value[i])) ;
                        else
                            ++i;
                        if (i < value.Length - 1)
                        {
                            sb.Append(value, i, value.Length - i);
                            yield return sb.ToString();
                        }
                        yield break;
                    }
                }
            }

            if (sb.Length > 0)
                yield return sb.ToString();
            else if (!removeEmptyEntries && predicate(value[value.Length - 1]))
                yield return string.Empty;
        }

        public static System.Collections.Generic.IEnumerable<string> SplitLazy(this string value, params char[] separator)
        {
            return value.SplitLazy(int.MaxValue, System.StringSplitOptions.None, separator);
        }

        public static System.Collections.Generic.IEnumerable<string> SplitLazy(this string value, System.StringSplitOptions options, params char[] separator)
        {
            return value.SplitLazy(int.MaxValue, options, separator);
        }

        public static System.Collections.Generic.IEnumerable<string> SplitLazy(this string value, int count, params char[] separator)
        {
            return value.SplitLazy(count, System.StringSplitOptions.None, separator);
        }

        public static bool IsNotNullOrEmpty(this string value) => string.IsNullOrEmpty(value) is false;

        public static bool IsNotNullOrWhitespace(this string value) => string.IsNullOrWhiteSpace(value) is false;
    }
}
