#region Copyright
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

namespace Media.Common.Extensions.TimeSpan
{
    /// <summary>
    /// Defines methods and properties for working with <see cref="System.TimeSpan"/>
    /// </summary>
    public static class TimeSpanExtensions
    {
        //(1e-6) = 0.000001
        
        //public static System.TimeSpan Undefined = System.TimeSpan.FromTicks(unchecked((long)double.NaN));
        public const double MicrosecondsPerCentisecond = 10,
            MicrosecondsPerMillisecond = 1000, //(1e+3)
            NanosecondsPerMicrosecond = MicrosecondsPerMillisecond,//1000,
            PicosecondsPerNanosecond = MicrosecondsPerMillisecond, //^^
            PicosecondsPerTick = 100000,//1e+5
            MicrosecondsPerPicosecond = 1000000, //1e+6
            TenMicrosecondsPerPicosecond = 10000000, //1e+7 = TimeSpan.TicksPerSecond = 0.01 Millisecond = 0.001 Centisecond = 0.0001 Decisecond = 10 Microsecond = 10000 Nanosecond = 10000000 Picosecond = (TimeSpan.TicksPerMillisecond = 10000)
            NanosecondsPerMillisecond = 1000000, //1e+6, MicrosecondsPerMillisecond * MicrosecondsPerMillisecond,             
            NanosecondsPerSecond = 1000000000, //1e+9
            PicosecondsPerJiffy = 1e+10,
            PicosecondsPerMilisecond = NanosecondsPerSecond, // ^^
            PicosecondsPerSecond = 10000000; // 1e+12

        public const decimal TicksPerNanosecond = 0.01m,//1e-2
            NanosecondsPerPicosecond = 0.001m,//1e-3  
            CentisecondsPerTick = 0.00001m, //1e-5
            TicksPerPicosecond = 0.0000001m, //1e-7
            MillisecondsPerPicosecond = 1e-9m, //1 Pico
            CentisecondsPerPicosecond = 1e-10m,
            JiffiesPerPicosecond = CentisecondsPerPicosecond,
            SecondsPerPicosecond = 1e-12m;

        /// <summary>  
        /// The number of ticks per Nanosecond.  
        /// </summary>  
        public const int NanosecondsPerTick = 100;

        /// <summary>
        /// The number of ticks per Microsecond.
        /// </summary>
        public const long TicksPerMicrosecond = 10;

        //const long would be a suitable replacement, then would use the .Ticks property of the instance.
        //public const long InfiniteTicks = -1;

        /// <summary>
        /// A <see cref="System.TimeSpan"/> with the value of -1 Millisecond
        /// </summary>
        public static readonly System.TimeSpan InfiniteTimeSpan = System.Threading.Timeout.InfiniteTimeSpan;

        /// <summary>
        /// A <see cref="System.TimeSpan"/> with the value of 1 Tick (100 ns)
        /// </summary>
        public static readonly System.TimeSpan OneTick = System.TimeSpan.FromTicks(1);

        /// <summary>
        /// A <see cref="System.TimeSpan"/> with the value of 2 Tick's (200 ns)
        /// </summary>
        public static readonly System.TimeSpan TwoHundedNanoseconds = System.TimeSpan.FromTicks(2);

        /// <summary>
        /// A <see cref="System.TimeSpan"/> with the value of 1 Second
        /// </summary>
        public static readonly System.TimeSpan OneSecond = System.TimeSpan.FromSeconds(1);

        /// <summary>
        /// A <see cref="System.TimeSpan"/> with the value of 1 Millisecond
        /// </summary>
        public static readonly System.TimeSpan OneMillisecond = InfiniteTimeSpan.Negate();

        /// <summary>
        /// A <see cref="System.TimeSpan"/> with the value of 1 Microsecond (μs)
        /// </summary>
        public static readonly System.TimeSpan OneMicrosecond = System.TimeSpan.FromTicks(TicksPerMicrosecond);

        /// <summary>
        /// A <see cref="System.TimeSpan"/> with the value of 1 Hour
        /// </summary>
        public static readonly System.TimeSpan OneHour = System.TimeSpan.FromHours(1);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static System.TimeSpan FromNanoseconds(double nanoSeconds)
        {
            //return System.TimeSpan.FromTicks((long)(nanoSeconds / NanosecondsPerTick));

            //Use the recripricol of 1/100
            return FromInterval((decimal)nanoSeconds, TicksPerNanosecond);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static System.TimeSpan FromInterval(decimal value, decimal ticksPerValue)
        {
            unchecked
            {
                return System.TimeSpan.FromTicks((long)(value * ticksPerValue));
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [System.CLSCompliant(false)]
        public static System.TimeSpan Min(ref System.TimeSpan a, ref System.TimeSpan b)
        {
            return a > b ? b : a;
        }

        public static System.TimeSpan Min(System.TimeSpan a, System.TimeSpan b) { return Min(ref a, ref b); }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [System.CLSCompliant(false)]
        public static System.TimeSpan Max(ref System.TimeSpan a, ref System.TimeSpan b)
        {
            return a > b ? a : b;
        }

        public static System.TimeSpan Max(System.TimeSpan a, System.TimeSpan b) { return Max(ref a, ref b); }
    }
}


namespace Media.UnitTests
{
    internal class TimeSpanExtensionsTests
    {
        public void TestFromMethods()
        {
            if (Common.Extensions.TimeSpan.TimeSpanExtensions.FromNanoseconds(0).Ticks != System.TimeSpan.Zero.Ticks) throw new System.Exception("FromNanoseconds");

            if (Common.Extensions.TimeSpan.TimeSpanExtensions.FromNanoseconds(10).Ticks != System.TimeSpan.Zero.Ticks) throw new System.Exception("FromNanoseconds");

            if (Common.Extensions.TimeSpan.TimeSpanExtensions.FromNanoseconds(99).Ticks != System.TimeSpan.Zero.Ticks) throw new System.Exception("FromNanoseconds");

            if (Common.Extensions.TimeSpan.TimeSpanExtensions.FromNanoseconds(100).Ticks != Common.Extensions.TimeSpan.TimeSpanExtensions.OneTick.Ticks) throw new System.Exception("FromNanoseconds");

            if (Common.Extensions.TimeSpan.TimeSpanExtensions.FromNanoseconds(200).Ticks != 2) throw new System.Exception("FromNanoseconds");

            if (Common.Extensions.TimeSpan.TimeSpanExtensions.OneMicrosecond.TotalNanoseconds != Common.Extensions.TimeSpan.TimeSpanExtensions.NanosecondsPerMicrosecond) throw new System.Exception("TotalNanoseconds");
        }
    }
}