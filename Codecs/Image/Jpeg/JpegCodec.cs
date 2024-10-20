using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Markup;
using Codec.Jpeg.Markers;
using Media.Codec.Interfaces;
using Media.Codecs.Image;
using Media.Common;

namespace Media.Codec.Jpeg
{
    public class JpegCodec : ImageCodec, IEncoder, IDecoder
    {
        const int ComponentCount = 3;

        public JpegCodec()
            : base("JPEG", Binary.ByteOrder.Little, ComponentCount, Binary.BitsPerByte)
        {
        }

        public override MediaType MediaTypes => MediaType.Image;

        public override bool CanEncode => true;

        public override bool CanDecode => true;

        public IEncoder Encoder => this;
        public IDecoder Decoder => this;

        public int Encode(JpegImage image, Stream outputStream)
        {
            var position = outputStream.Position;
            image.Save(outputStream);
            return (int)(outputStream.Position - position);
        }

        public JpegImage Decode(Stream inputStream)
            => JpegImage.FromStream(inputStream);

        public static IEnumerable<Marker> ReadMarkers(Stream jpegStream)
        {
            int streamOffset = 0;
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

                switch (FunctionCode)
                {
                    case Markers.StartOfInformation:
                    case Markers.EndOfInformation:
                        goto AtMarker;
                }

                //Read Length Bytes
                if (Binary.BytesPerShort != jpegStream.Read(sizeBytes))
                    throw new InvalidDataException("Not enough bytes to read marker Length.");

                //Calculate Length
                CodeSize = Binary.ReadU16(sizeBytes, 0, Binary.IsLittleEndian);

                if (CodeSize < 0)
                    CodeSize = Binary.ReadU16(sizeBytes, 0, Binary.IsBigEndian);

            AtMarker:
                var Current = new Marker((byte)FunctionCode, CodeSize);

                jpegStream.Read(Current.Data.Array, Current.Data.Offset, Current.DataSize);

                if (Current.FunctionCode == Markers.StartOfScan)
                    TryProcessStartofScan(Current);

                streamOffset += Current.DataSize;

                yield return Current;

                CodeSize = 0;
            }
        }

        private static bool TryProcessStartofScan(Marker marker)
        {
            if (marker.FunctionCode != Markers.StartOfScan || marker.DataSize <= 0)
                return false;
                 
            var bitOffset = Binary.BytesToBits(marker.Data.Offset);

            //Read the number of components
            var Ns = Binary.ReadBits(marker.Data.Array, ref bitOffset, Binary.BitsPerByte, Binary.BitOrder.MostSignificant);

            if (Binary.BitsToBytes(ref bitOffset) >= marker.Count)
                return false;

            List<ValueTuple<long, long, long>> test = new();

            //Read the components (Csj)
            for (int j = 0; j < Ns; ++j)
            {
                var Csj = Binary.ReadBits(marker.Data.Array, ref bitOffset, Binary.BitsPerByte, Binary.BitOrder.MostSignificant);
                if (Binary.BitsToBytes(ref bitOffset) >= marker.Count)
                    return false;

                //Read the entropy coding table selectors DC nybble
                var Tdj = Binary.ReadBits(marker.Data.Array, ref bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
                if (Binary.BitsToBytes(ref bitOffset) >= marker.Count)
                    return false;

                //Read the entropy coding table selectors AC nybble
                var Taj = Binary.ReadBits(marker.Data.Array, ref bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
                if (Binary.BitsToBytes(ref bitOffset) >= marker.Count)
                    return false;

                test.Add((Csj, Tdj, Taj));
            }

            //Read the Ss byte
            var Ss = Binary.ReadBits(marker.Data.Array, ref bitOffset, Binary.BitsPerByte, Binary.BitOrder.MostSignificant);

            if (Binary.BitsToBytes(ref bitOffset) >= marker.Count)
                return false;

            //Read the Se byte
            var Se = Binary.ReadBits(marker.Data.Array, ref bitOffset, Binary.BitsPerByte, Binary.BitOrder.MostSignificant);

            if (Binary.BitsToBytes(ref bitOffset) >= marker.Count)
                return false;

            //Read the Ah nybble
            var Ah = Binary.ReadBits(marker.Data.Array, ref bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);

            if (Binary.BitsToBytes(ref bitOffset) >= marker.Count)
                return false;

            //Read the Al nybble
            var Al = Binary.ReadBits(marker.Data.Array, ref bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);

            var StartOfScanMarker = new StartOfScanMarker(marker);

            if (StartOfScanMarker.Ns != Ns)
                return false;

            if (StartOfScanMarker.Ss != Ss)
                return false;

            if (StartOfScanMarker.Se != Se)
                return false;

            if (StartOfScanMarker.Ah != Ah)
                return false;

            if (StartOfScanMarker.Al != Al)
                return false;

            var managedComponents = StartOfScanMarker.Components.ToList();

            if (managedComponents.Count != Ns)
                return false;

            for(var i = 0; i < Ns; ++i)
            {
                var component = managedComponents[i];
                var testComponent = test[i];
                if (component.ScanComponentSelector != testComponent.Item1 ||
                    component.EntropyCodingTableSelectorDC != testComponent.Item2 ||
                    component.EntropyCodingTableSelectorAC != testComponent.Item3)
                    return false;
            }

            return true;
        }
    }
}