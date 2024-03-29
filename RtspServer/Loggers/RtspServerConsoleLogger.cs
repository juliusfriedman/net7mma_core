﻿/*
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

using System;

namespace Media.Rtsp.Server.Loggers
{
    public class RtspServerConsoleLogger : RtspServerLogger
    {
        public string Format = "{0} {1} {2} {3} {4}\r\n";

        public ConsoleColor RequestColor = ConsoleColor.Cyan, ResponseColor = ConsoleColor.DarkCyan, ExceptionColor = ConsoleColor.Red, NormalColor = ConsoleColor.Green;

        //This could be a DebuggingLogger or something else...
        private readonly Media.Common.ILogging Logger = new Media.Common.Loggers.ConsoleLogger();

        internal override void LogRequest(RtspMessage request, ClientSession session)
        {
            ConsoleColor previous = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = RequestColor;
                Logger.Log(string.Format(Format, "Request=>", request, "Session=>", session.Id, null));
            }
            finally { Console.ForegroundColor = previous; }
        }

        internal override void LogResponse(RtspMessage response, ClientSession session)
        {
            ConsoleColor previous = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ResponseColor;
                Logger.Log(string.Format(Format, "Response=>", response, "Session=>", session.Id, null));
            }
            finally { Console.ForegroundColor = previous; }
        }

        public override void LogException(Exception ex)
        {
            ConsoleColor previous = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ExceptionColor;
                Logger.Log(string.Format(Format, ex.Message, Environment.NewLine, ex.StackTrace, Environment.NewLine, ex.InnerException is not null ? ex.InnerException.ToString() : string.Empty));
            }
            finally { Console.ForegroundColor = previous; }
        }

        public override void Log(string data)
        {
            ConsoleColor previous = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = NormalColor;
                Console.WriteLine(data);
            }
            finally { Console.ForegroundColor = previous; }
        }

        public override void Dispose()
        {
            Logger.Dispose();

            base.Dispose();
        }
    }
}
