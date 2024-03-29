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

namespace Media.Common
{
    /// <see href="https://www.asciitable.com/">The ASCII Table</see>
    [System.CLSCompliant(true)]
    public sealed class ASCII
    {
        /// <summary>
        /// An instance of <see cref="System.Text.ASCIIEncoding"/>
        /// </summary>
        public static readonly System.Text.Encoding ASCIIEncoding = new System.Text.ASCIIEncoding();

        public const byte Null = 0x00,
            Bell = 0x07, // `\a` => 07 Decimal`
            Backspace = 0x08, //`\b` => 08 Decimal`
            HorizontalTab = 0x09, // `\t` => 09 Decimal`
            LineFeed = 0x0A, // NewLine `\n` => 10 Decimal
            VerticalTab = 0x0B, // `\v` => 11 Decimal
            FormFeed = 0x0C, // NewPage `\f` => 12 Decimal
            NewLine = 0x0D, // `\r` => 13 Decimal
                            //ShiftOut 14
                            //ShiftIn 15
                            //DataLinkEscape 16
            Escape = 0x1B, // `\e` => 27 Decimal
            Space = 0x20,// ` ` => 32 Decimal
            /*....*/
            EqualsSign = 0x3D, // =
            HyphenSign = 0x2D, // -
            Comma = 0x2C, // ,
            Period = 0x2E, // .
            ForwardSlash = 0x2F, // '/'
            Colon = 0x3A, // :
            SemiColon = 0x3B, // ;
            AtSign = 0x40, // @
            BackSlash = 0x5C, // '\'
            DoubleQuote = 0x22,// '"'
            SingleQuote = 0x27, // '\''
            Asterisk = 0x2A; // '*'

        //OpenParenthesis, CloseParenthesis

        //static char[] LineEndingCharacters = System.Text.Encoding.ASCII.GetChars(new byte[] { NewLine, LineFeed });

        //WhiteSpaceCharacters ...

        /// <summary>
        /// Determines if the given <see cref="char"/> is inclusively within the range of `0-9, A-F and a-f` .
        /// </summary>
        /// <param name="c">the char</param>
        /// <returns>True if the character is within the alloted range, otherwise false.</returns>
        public static bool IsHexDigit(char c) { return IsHexDigit(ref c); }

        /// <summary>
        /// Determines if the given <see cref="char"/> is inclusively within the range of `0-9, A-F and a-f` .
        /// </summary>
        /// <param name="c">the reference to the car</param>
        /// <returns>True if the character is within the alloted range, otherwise false.</returns>
        [System.CLSCompliant(false)] //decimal values used describe 0 - 9,            A - F,                  a - f
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool IsHexDigit(ref char c)
        {
            return (c >= 48 && c <= 57) | (c >= 65 && c <= 70) | (c >= 97 && c <= 102);

            //return (c >= 48 && c <= 57) || (c >= 65 && c <= 70) || (c >= 97 && c <= 102);

            //switch ((int)c)
            //{
            //    default: return false;
            //    case 48: // (c >= 48 && c <= 57)
            //    case 49:
            //    case 50:
            //    case 51:
            //    case 52:
            //    case 53:
            //    case 54:
            //    case 55:
            //    case 56:
            //    case 57: // (c >= 65 && c <= 70)
            //    case 65:
            //    case 66:
            //    case 67:
            //    case 68:
            //    case 69:
            //    case 70: // (c >= 97 && c <= 102) //May need to seperate switch here to improve cache locality..
            //    case 97:
            //    case 98:
            //    case 99:
            //    case 100:
            //    case 101:
            //    case 102: return true;
            //}


        }

        #region Number Extraction

        //Should be in Text extensions which specify the encoding.

        public static string ExtractPrecisionNumber(string input, char sign = (char)Common.ASCII.Period)
        {
            return string.IsNullOrWhiteSpace(input)
                ? throw new System.InvalidOperationException("input cannot be null or consist only of whitespace.")
                : ASCII.ExtractPrecisionNumber(input, 0, input.Length, sign);
        }

        public static string ExtractPrecisionNumber(string input, int offset, int length, char sign = (char)Common.ASCII.Period)
        {
            return ASCII.ExtractNumber(input, offset, length, sign);
        }

        public static string ExtractNumber(string input, char? sign = null)
        {
            return string.IsNullOrWhiteSpace(input)
                ? throw new System.InvalidOperationException("input cannot be null or consist only of whitespace.")
                : ASCII.ExtractNumber(input, 0, input.Length, sign);
        }

        public static string ExtractNumber(string input, int offset, int length, char? sign = null)
        {
            if (string.IsNullOrWhiteSpace(input)) throw new System.InvalidOperationException("input cannot be null or consist only of whitespace.");

            try
            {
                //Make a builder to extract the number
                System.Text.StringBuilder output = new(input.Length);

                //Keep track of the sign if it was found
                bool foundSign = false == sign.HasValue;

                //Iterate the characters indicated.
                for (; offset < length; ++offset)
                {
                    //Look at the character
                    char c = input[offset];

                    //If its the sign
                    if (sign.HasValue && c == sign)
                    {
                        //If it was not found already
                        if (false == foundSign)
                        {
                            //Include it
                            foundSign = true;

                            output.Append(c);
                        }

                        //Skip
                        continue;
                    }

                    //If the value contains what is possibly realted to the number then append it.
                    //if (false == char.IsDigit(c)) continue;

                    //If the value is not a hex digit then do not allow it
                    if (false == ASCII.IsHexDigit(ref c)) continue;

                    //Append the char
                    output.Append(c);
                }

                //Return the string.
                return output.ToString();
            }
            catch
            {
                throw;
            }
        }

        #endregion

        #region Extras (SharpOS)

        public static byte ToLower(byte ch)
        {
            return ch is >= ((byte)'A') and <= ((byte)'Z') ? (byte)(ch - ((byte)'A' - (byte)'a')) : ch;
        }

        public static byte ToUpper(byte ch)
        {
            return ch is >= ((byte)'a') and <= ((byte)'z') ? (byte)(ch - ((byte)'a' - (byte)'A')) : ch;
        }

        public static bool IsLowerAlpha(byte ch)
        {
            return ch is >= ((byte)'a') and <= ((byte)'z');
        }

        public static bool IsUpperAlpha(byte ch)
        {
            return ch is >= ((byte)'A') and <= ((byte)'Z');
        }

        public static bool IsAlpha(byte ch)
        {
            return IsLowerAlpha(ch) || IsUpperAlpha(ch);
        }

        public static bool IsBackspace(byte ch)
        {
            return ch == 26;
        }

        public static bool IsNumeric(byte ch)
        {
            return ch is >= ((byte)'0') and <= ((byte)'9');
        }

        public static bool IsWhiteSpace(byte ch)
        {
            return ch switch
            {
                (byte)' ' => true,
                (byte)'\n' => true,
                (byte)'\r' => true,
                (byte)'\v' => true,
                (byte)'\f' => true,
                (byte)'\t' => true,
                _ => false,
            };
        }

        #endregion
    }
}
