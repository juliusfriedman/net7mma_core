using Media.Common;
using System;
using System.Collections.Generic;
using System.IO;

namespace Media.Codec.Jpeg
{

    //Would be helpful to have a Stream with a buffer for skipping

    //Should allow marker reading on it's own outside of the RFC2435 class to decouple logic.

    //Should be a MediaContainer

    public class MarkerReader : IDisposable
    {
        private System.IO.Stream jpegStream;
        private int streamOffset;
        private readonly int streamLength;
        private readonly bool leaveOpen;

        public Marker Current;

        public int Remains => streamLength - streamOffset;

        public MarkerReader(System.IO.Stream stream, bool leaveOpen = true)
        {
            jpegStream = stream;
            streamLength = (int)stream.Length;
            this.leaveOpen = leaveOpen;
        }

        public IEnumerable<Marker> ReadMarkers()
        {
            int FunctionCode, CodeSize = 0;
            byte[] sizeBytes = new byte[Binary.BytesPerShort];

            //Find a Jpeg Tag while we are not at the end of the stream
            //Tags come in the format 0xFFXX
            while ((FunctionCode = jpegStream.ReadByte()) != -1)
            {
                ++streamOffset;

                //If the byte is a prefix byte then continue

                if (FunctionCode is 0 || FunctionCode is Markers.Prefix)
                    continue;

                if (FunctionCode is Markers.StartOfInformation)
                    goto AtMarker;

                //Read Length Bytes
                if(Binary.BytesPerShort != jpegStream.Read(sizeBytes))
                    throw new InvalidDataException("Not enough bytes to read marker Length.");

                //Calculate Length
                CodeSize = Binary.ReadU16(sizeBytes, 0, Binary.IsLittleEndian);

                if (CodeSize > Remains)
                    CodeSize = Binary.ReadU16(sizeBytes, 0, Binary.IsBigEndian);

                AtMarker:
                Current = new Marker((byte)FunctionCode, CodeSize);

                jpegStream.Read(Current.Data.Array, Current.Data.Offset, Current.DataSize);

                streamOffset += Current.DataSize;

                yield return Current;

                CodeSize = 0;
            }
        }

        public void Dispose()
        {
            if (jpegStream is not null && leaveOpen == false)
                jpegStream.Dispose();
            jpegStream = null;
        }
    }    
}
