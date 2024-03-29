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

namespace Media.Common.Extensions.Linq
{
    public static class LinqExtensions
    {
        public static System.Collections.Generic.IEnumerable<T> Yield<T>(this T t) { yield return t; }

        public static System.Collections.Generic.IEnumerable<T> Concat<T>(this System.Collections.Generic.IEnumerable<T> a, T b)
        {
            return System.Linq.Enumerable.Concat(a, Yield(b));
        }

        public static System.Collections.Generic.IEnumerable<T> Prefix<T>(this System.Collections.Generic.IEnumerable<T> a, T b)
        {
            return System.Linq.Enumerable.Concat(Yield(b), a);
        }

        public static System.Collections.Generic.IEnumerable<T> Prefix<T>(this System.Collections.Generic.IEnumerable<T> a, System.Collections.Generic.IEnumerable<T> b)
        {
            return System.Linq.Enumerable.Concat(b, a);
        }
    }
}
