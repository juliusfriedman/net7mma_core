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

using System;

namespace Media.Rtsp.Server.Loggers
{
    public class RtspServerDebugLogger : RtspServerLogger
    {
        public string Format = "{0} {1} {2} {3} {4}\r\n";
        private readonly Media.Common.ILogging Logger = new Media.Common.Loggers.DebugLogger();

        internal override void LogRequest(RtspMessage request, ClientSession session)
        {
            Logger.Log(
                string.Format(Format, request.RtspMessageType, request.RtspMethod, request.Location, session.Id, null));
        }

        internal override void LogResponse(RtspMessage response, ClientSession session)
        {
            Logger.Log(
                string.Format(Format, response.RtspMessageType, response.CSeq, response.RtspStatusCode, session.Id, null));
        }

        public override void LogException(Exception ex)
        {
            Logger.Log(
                string.Format(Format, ex.Message, Environment.NewLine, ex.StackTrace, Environment.NewLine, ex.InnerException != null ? ex.InnerException.ToString() : string.Empty));
        }

        public override void Log(string data)
        {
            Logger.Log(data);
        }

        public override void Dispose()
        {
            Logger.Dispose();

            base.Dispose();
        }
    }
}
