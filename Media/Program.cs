using System;

namespace Media
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

        }

        static void TestRtpDumpReader(string path, Media.RtpTools.FileFormat? knownFormat = null)
        {
            //Always use an unknown format for the reader allows each item to be formatted differently
            using (Media.RtpTools.RtpDump.DumpReader reader = new Media.RtpTools.RtpDump.DumpReader(path, knownFormat))
            {


            }
        }
    }
}
