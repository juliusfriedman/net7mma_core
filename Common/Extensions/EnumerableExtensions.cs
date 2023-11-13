#region Copyright
/*
This file came from Managed Media Aggregation, You can always find the latest version @ https://net7mma.codeplex.com/
  
 Julius.Friedman@gmail.com / (SR. Software Engineer ASTI Transportation Inc. http://www.asti-trans.com)

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

namespace Media.Common.Extensions
{
    public static class EnumerableExtensions
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool SequenceEquals(this System.Collections.IEnumerable left, System.Collections.IEnumerable right)
        {
            if (left is null) return right is null;

            System.Collections.IEnumerator one, two;
            System.IDisposable weird = null, strnage = null;

            try
            {
                using (weird = (one = left.GetEnumerator()) as System.IDisposable)
                {
                    using (strnage = (two = right.GetEnumerator()) as System.IDisposable)
                    {
                        while (one.MoveNext())
                        {
                            if (two.MoveNext() is false ||
                                one.Current.Equals(two.Current) is false) return false;
                        }

                        return true;
                    }
                }
            }
            catch
            {
                throw;
            }
            finally
            {
                strnage?.Dispose();
                weird?.Dispose();
            }
        }


        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool SequenceEquals<T>(this System.Collections.Generic.IEnumerable<T> left, System.Collections.Generic.IEnumerable<T> right)
        {
            if (left is null) return right is null;

            using var weird = left.GetEnumerator();
            using var strnage = right.GetEnumerator();

            while (weird.MoveNext())
            {
                if (strnage.MoveNext() is false ||
                    weird.Current.Equals(strnage.Current) is false) return false;
            }

            return true;
        }
    }
}
