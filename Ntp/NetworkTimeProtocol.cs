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

namespace Media.Ntp
{
    /// <summary>
    /// Contains logic useful for calculating values which correspond to the <see href="http://en.wikipedia.org/wiki/Network_Time_Protocol"> Network Time Protocol </see>
    /// </summary>
    [System.CLSCompliant(true)]
    public class NetworkTimeProtocol
    {
        //Should all probably be DateTimeOffset

        /// <summary>
        /// Converts specified DateTime value to short NPT time.
        /// </summary>
        /// <param name="value">DateTime value to convert.</param>
        /// <returns>Returns NPT value.</returns>
        /// <notes>
        /// In some fields where a more compact representation is
        /// appropriate, only the middle 32 bits are used; that is, the low 16
        /// bits of the integer part and the high 16 bits of the fractional part.
        /// The high 16 bits of the integer part must be determined independently.
        /// </notes>
        [System.CLSCompliant(false)]
        //public static uint DateTimeToNptTimestamp32(ref System.DateTime value) { return (uint)((DateTimeToNptTimestamp(ref value) << Common.Binary.BitsPerShort) & uint.MaxValue); }
        public static uint DateTimeToNptTimestamp32(ref System.DateTime value) { return (uint)(DateTimeToNptTimestamp(ref value) >> Common.Binary.BitsPerInteger); } //otherwise would equal frac..

        [System.CLSCompliant(false)]
        public static uint DateTimeToNptTimestamp32(System.DateTime value) { return DateTimeToNptTimestamp32(ref value); }

        //Error	44	Type 'Media.Ntp.NetworkTimeProtocol' already defines a member called 'DateTimeToNptTimestamp' with the same parameter types
        //public static long DateTimeToNptTimestamp(System.DateTime value) { return (long)DateTimeToNptTimestamp(ref value); }

        [System.CLSCompliant(false)]
        public static ulong DateTimeToNptTimestamp(System.DateTime value) { return DateTimeToNptTimestamp(ref value); }

        /// <summary>
        /// Converts specified DateTime value to long NPT time.
        /// </summary>
        /// <param name="value">DateTime value to convert. This value must be in local time.</param>
        /// <returns>Returns NPT value.</returns>
        /// <notes>
        /// Wallclock time (absolute date and time) is represented using the
        /// timestamp format of the Network Time Protocol (NPT), which is in
        /// seconds relative to 0h UTC on 1 January 1900 [4].  The full
        /// resolution NPT timestamp is a 64-bit unsigned fixed-point number with
        /// the integer part in the first 32 bits and the fractional part in the
        /// last 32 bits. In some fields where a more compact representation is
        /// appropriate, only the middle 32 bits are used; that is, the low 16
        /// bits of the integer part and the high 16 bits of the fractional part.
        /// The high 16 bits of the integer part must be determined independently.
        /// </notes>
        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static ulong DateTimeToNptTimestamp(ref System.DateTime value)//, bool randomize = false) //todo, allow pass epoc or bool use epoc...
        {
            System.DateTime baseDate = value >= UtcEpoch2036 ? UtcEpoch2036 : UtcEpoch1900;

            System.TimeSpan elapsedTime = value > baseDate ? value.ToUniversalTime() - baseDate.ToUniversalTime() : baseDate.ToUniversalTime() - value.ToUniversalTime();

            //Media.Common.Extensions.TimeSpan.TimeSpanExtensions.MicrosecondsPerMillisecond = 1000

            //TicksPerPicosecond = 0.0000001m = 1e-7

            //4294967296 = uint.MaxValue + 1

            //0.001 == PicosecondsPerNanosecond = 1e-3            

            //429496.7296 Picoseconds = 4.294967296e-7 Seconds

            //4.294967296e-7 * 1000 Milliseconds per second = 0.0004294967296 * 1e+9 (PicosecondsPerMilisecond) = 429.4967296

            //0.4294967296 nanoseconds * 100 nanoseconds = 1 tick = 42.94967296 * 10000 ticks per millisecond = 429496.7296 / 1000 = 429.49672960000004

            unchecked
            {
                //return (ulong)((long)(elapsedTime.Ticks * 0.0000001m) << 32 | (long)((decimal)elapsedTime.TotalMilliseconds % 1000 * 4294967296m * 0.001m));
                //return (ulong)(((long)(elapsedTime.Ticks * 0.0000001m) << 32) + (elapsedTime.TotalMilliseconds % 1000 * 4294967296ul * 0.001));
                //return (ulong)(elapsedTime.Ticks * 1e-7 * 4294967296ul); //ie-7 * 4294967296ul = 429.4967296 has random diff which complies better? (In order to minimize bias and help make timestamps unpredictable to an intruder, the non - significant bits should be set to an unbiased random bit string.)
                //return (ulong)(elapsedTime.Ticks * 429.4967296m);//decimal precision is better but we still lose precision because of the magnitude? 0.001 msec dif ((ulong)(elapsedTime.Ticks * 429.4967296000000000429m))
                //429.49672960000004m has reliable 003 msec diff
                //Has 0 diff but causes fraction to be different from examples...
                //return (ulong)((elapsedTime.Ticks + 1) * 429.4967296m);
                //Also adding + 429ul;                
                return (ulong)(elapsedTime.Ticks * 429.496729600000000000429m);
                //double with precision loss around certain measures.
                //return (ulong)((elapsedTime.Ticks * 429.4967296) + ((uint)elapsedTime.Ticks * 0.0000001));

                //return (ulong)(elapsedTime.Ticks * 429.49672960000005);//m has wrong frac
                //var ticks =  (ulong)(elapsedTime.Ticks * 429.496729600000000000429m); //Has 0 diff on .137 measures otherwise 0.001 msec or 1 tick, keeps the examples the same.
                //if(randomize) ticks ^= (ulong)(Utility.Random.Next() & byte.MaxValue);
                //return ticks;

                //(ulong)(elapsedTime.Ticks * 429.496729600000000429429m)
                //12992241732673339404
                //return (ulong)(elapsedTime.Ticks * 429.4967296000000000429429m) - 1;
                //12992241732673339393 - 1
                //return (ulong)(elapsedTime.Ticks * 429.49672960000000005006007m) - 1;
                //12992241732673339393 - 1

                //12992241732673339392 is correct example.

            }

            //https://stackoverflow.com/questions/16763300/converting-between-ntp-and-c-sharp-datetime/54067805#54067805
            //decimal b = value.Ticks - (value >= UtcEpoch2036 ? UtcEpoch2036.Ticks : UtcEpoch1900.Ticks);
            //b = (decimal)b / 1e7m * (4294967296ul);
            //return (ulong)b;
        }

        [System.CLSCompliant(false)]
        public static System.DateTime NptTimestampToDateTime(ref ulong nptTimestamp)
        {
            return NptTimestampToDateTime((uint)((nptTimestamp >> Common.Binary.BitsPerInteger) & uint.MaxValue), (uint)(nptTimestamp & uint.MaxValue));
        }

        [System.CLSCompliant(false)]
        public static System.DateTime NptTimestampToDateTime(ulong ntpTimestamp) { return NptTimestampToDateTime(ref ntpTimestamp); }

        public static System.DateTime NptTimestampToDateTime(long ntpTimestamp) { return NptTimestampToDateTime((ulong)ntpTimestamp); }

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static System.DateTime NptTimestampToDateTime(ref uint seconds, ref uint fractions, System.DateTime? epoch = null)
        {
            //Convert to ticks
            //ulong ticks = (ulong)((seconds * System.TimeSpan.TicksPerSecond) + ((fractions * System.TimeSpan.TicksPerSecond) / 0x100000000L)); //uint.MaxValue + 1

            unchecked
            {
                //Convert to ticks,                 

                //'UtcEpoch1900.AddTicks(seconds * System.TimeSpan.TicksPerSecond + ((long)(fractions * 1e+12))).Millisecond' threw an exception of type 'System.ArgumentOutOfRangeException'

                //0.01 millisecond = 1e+7 picseconds = 10000 nanoseconds
                //10000 nanoseconds = 10 micros = 10000000 pioseconds
                //0.001 Centisecond = 10 Microsecond
                //1 Tick = 0.1 Microsecond
                //0.1 * 100 Nanos Per Tick = 100
                                                                                            //System.TimeSpan.TicksPerSecond is fine here also...
                long ticks = seconds * System.TimeSpan.TicksPerSecond + ((long)(fractions * Media.Common.Extensions.TimeSpan.TimeSpanExtensions.TenMicrosecondsPerPicosecond) >> Common.Binary.BitsPerInteger);

                //Adding a tick here can make the diff 0

                //System.TimeSpan.FromMilliseconds(0.1) .ticks == 0
                //System.TimeSpan.FromMilliseconds(1e-4) == System.TimeSpan.Zero

                //(long)((decimal)seconds * System.TimeSpan.TicksPerSecond + (decimal)(fractions * 100000m)) doesn't work...

                //(long)((decimal)seconds * System.TimeSpan.TicksPerSecond + ((long)(decimal)(fractions * 100000m) >> 32)) is the wrong scale for the fractions, hence math in different terms above.

                //Return the result of adding the ticks to the epoch
                //If the epoch was given then use that value otherwise determine the epoch based on the highest bit.
                return epoch.HasValue ? epoch.Value.AddTicks(ticks) :
                        (seconds & 0x80000000L) == 0 ?
                            UtcEpoch2036.AddTicks(ticks) :
                                UtcEpoch1900.AddTicks(ticks);
            }
        }

        [System.CLSCompliant(false)]
        public static System.DateTime NptTimestampToDateTime(uint seconds, uint fractions, System.DateTime? epoch = null) { return NptTimestampToDateTime(ref seconds, ref fractions, epoch); }

        public static System.DateTime NptTimestampToDateTime(int seconds, int fractions, System.DateTime? epoch = null)
        {
            uint sec = (uint)seconds, frac = (uint)fractions;

            return NptTimestampToDateTime(ref sec, ref frac, epoch);
        }

        /// <summary>
        /// The seconds difference in seconds between NTP Time and Unix Time.
        /// </summary>
        public const long NtpUnixDifferenceSeconds = 2208988800;

        /// <summary>
        /// The <see cref="System.TimeSpan"/> which represents <see cref="NtpUnixDifferenceSeconds"/>
        /// </summary>
        public static System.TimeSpan NtpUnixDifference = System.TimeSpan.FromSeconds(NtpUnixDifferenceSeconds);

        //When the First Epoch will wrap (The real Y2k)
        public static System.DateTime UtcEpoch2036 = new System.DateTime(2036, 2, 7, 6, 28, 16, System.DateTimeKind.Utc);

        public static System.DateTime UtcEpoch1900 = new System.DateTime(1900, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);

        public static System.DateTime UtcEpoch1970 = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
    }
}


namespace Media.UnitTests
{
    /// <summary>
    /// Provides tests which ensure the logic of the <see cref="Media.Ntp.NetworkTimeProtocol"/> class is correct
    /// </summary>
    internal class NetworkTimeProtocolUnitTests
    {

        /// <summary>
        /// Tests an example provided in RFC3550
        /// </summary>
        public static void TestNptTimestampToDateTime_And_Reverse()
        {
            ulong test = 0xb44d_b705_2000_0000;

            System.DateTime result = Media.Ntp.NetworkTimeProtocol.NptTimestampToDateTime(ref test);

            System.DateTime expected = new System.DateTime(1995, 11, 10, 11, 33, 25, 125, System.DateTimeKind.Utc);

            if (result != expected) throw new System.Exception("Incorrect date: " + result);

            //msw                          //lsw
            uint sec = (uint)(test >> 32), frac = (uint)(test & uint.MaxValue);

            if (sec != 3024992005) throw new System.Exception("Wrong Second");

            if (frac != 536870912) throw new System.Exception("Wrong Fraction");

            result = Media.Ntp.NetworkTimeProtocol.NptTimestampToDateTime(ref sec, ref frac);

            if (result != expected) throw new System.Exception("Incorrect date: " + result);

            ulong reverse = Media.Ntp.NetworkTimeProtocol.DateTimeToNptTimestamp(ref result);

            //Test the sec part
            if (reverse >> 32 != sec) throw new System.Exception("DateTimeToNptTimestamp sec");

            //Test the frac part
            if ((uint)reverse != frac) throw new System.Exception("DateTimeToNptTimestamp frac");

            //reverse should equal 12992241732673339392 (test) = 0xb44d_b705_2000_0000

            if (reverse != test) throw new System.Exception("DateTimeToNptTimestamp:" + reverse + ", Error: " + (test - reverse)); 

            reverse = Media.Ntp.NetworkTimeProtocol.DateTimeToNptTimestamp32(ref result);

            if (reverse != sec) throw new System.Exception("DateTimeToNptTimestamp32" + reverse + ", Error: " + (test - reverse));
        }

        /// <summary>
        /// O( )
        /// </summary>
        public static void TestRoundTrip()
        {
            System.DateTime now = System.DateTime.UtcNow;

            ulong ntpTimestamp = Media.Ntp.NetworkTimeProtocol.DateTimeToNptTimestamp(now);

            System.Console.WriteLine("DateTime.UtcNow = " + now.ToString("s.fffffff"));

            System.Console.WriteLine("DateTimeToNptTimestamp(now) = " + ntpTimestamp);

            System.DateTime fromTimeStamp = Media.Ntp.NetworkTimeProtocol.NptTimestampToDateTime(ref ntpTimestamp);

            System.Console.WriteLine("DateTimeToNptTimestamp(ref ntpTimestamp) = " + fromTimeStamp.ToString("s.fffffff"));

            System.Console.WriteLine("DateTimeToNptTimestamp(fromTimeStamp) = " + Media.Ntp.NetworkTimeProtocol.DateTimeToNptTimestamp(fromTimeStamp));

            var diff = (fromTimeStamp - now).Duration();

            System.Console.WriteLine("Different by " + diff.TotalMilliseconds + " Milliseconds");

            System.Console.WriteLine("Different by " + diff.Ticks + " Ticks");

            if (diff.TotalSeconds > 1.0) throw new System.Exception("Cannot round trip NTP");

            ulong roundTrip = Media.Ntp.NetworkTimeProtocol.DateTimeToNptTimestamp(fromTimeStamp);

            System.Console.WriteLine("rountTrip = " + roundTrip);
            System.Console.WriteLine("diff = " + (ntpTimestamp - roundTrip));
            System.Console.WriteLine("diff = " + (roundTrip - ntpTimestamp));

            if (roundTrip != Media.Ntp.NetworkTimeProtocol.DateTimeToNptTimestamp(fromTimeStamp)) throw new System.Exception("roundTrip = " + roundTrip);

            ntpTimestamp = (ulong)(Media.Ntp.NetworkTimeProtocol.DateTimeToNptTimestamp32(now)) << Media.Common.Binary.BitsPerInteger;

            System.Console.WriteLine("DateTimeToNptTimestamp32(now) = " + ntpTimestamp);

            fromTimeStamp = Media.Ntp.NetworkTimeProtocol.NptTimestampToDateTime(ref ntpTimestamp);

            roundTrip = Media.Ntp.NetworkTimeProtocol.DateTimeToNptTimestamp(fromTimeStamp);

            if (roundTrip != Media.Ntp.NetworkTimeProtocol.DateTimeToNptTimestamp(fromTimeStamp)) throw new System.Exception("roundTrip32 = " + roundTrip);

            System.Console.WriteLine("DateTimeToNptTimestamp(ref ntpTimestamp) = " + fromTimeStamp.ToString("s.fffffff"));

            System.Console.WriteLine("DateTimeToNptTimestamp(fromTimeStamp) = " + Media.Ntp.NetworkTimeProtocol.DateTimeToNptTimestamp(fromTimeStamp));

            var diff32 = (fromTimeStamp - now).Duration();

            System.Console.WriteLine("Different by " + diff32.TotalMilliseconds + " Milliseconds");

            System.Console.WriteLine("Different by " + diff32.Ticks + " Ticks");

            if (diff32.TotalSeconds > 1.0) throw new System.Exception("Cannot round trip NTP 32");

            System.Console.WriteLine("rountTrip = " + roundTrip);
            System.Console.WriteLine("diff = " + (ntpTimestamp - roundTrip));
            System.Console.WriteLine("diff = " + (roundTrip - ntpTimestamp));

            var diff3 = (diff32 - diff);

            System.Console.WriteLine("Total Difference of " + diff3.TotalMilliseconds + " Milliseconds");

            System.Console.WriteLine("Total Difference of " + diff3.Ticks + " Ticks");
        }

        //public static void TestRoundTrip_Multi()
        //{
        //    for (int i = 0; i < 50; ++i) TestRoundTrip();
        //    System.Linq.ParallelEnumerable.ForAll(System.Linq.ParallelEnumerable.AsParallel(System.Linq.Enumerable.Range(0, 100)), i => TestRoundTrip());
        //    for (int i = 0; i < 50; ++i) TestRoundTrip();
        //}
    }
}