using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Codec.Jpeg.Classes;
using Codec.Jpeg.Markers;
using Media.Codec.Interfaces;
using Media.Codecs.Image;
using Media.Common;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Media.Codec.Jpeg
{
    public class JpegCodec : ImageCodec, IEncoder, IDecoder
    {
        public const int ComponentCount = 4;

        public static ImageFormat DefaultImageFormat
        {
            get => new
                (
                    Binary.ByteOrder.Big,
                    DataLayout.Packed,
                    new JpegComponent(0, 1, Binary.BitsPerByte),
                    new JpegComponent(1, 2, Binary.BitsPerByte),
                    new JpegComponent(2, 3, Binary.BitsPerByte),
                    new JpegComponent(3, 4, Binary.BitsPerByte)
                );
        }

        public JpegCodec()
            : base("JPEG", Binary.ByteOrder.Little, ComponentCount, Binary.BitsPerByte)
        {
        }

        public override MediaType MediaTypes => MediaType.Image;

        public override bool CanEncode => true;

        public override bool CanDecode => true;

        public IEncoder Encoder => this;
        public IDecoder Decoder => this;

        public int Encode(JpegImage image, Stream outputStream, int quality = 0)
        {
            var position = outputStream.Position;
            image.Save(outputStream, quality);
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
                if (FunctionCode is 0 || FunctionCode is Markers.Prefix || false == Markers.IsKnownFunctionCode((byte)FunctionCode))
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

                if (CodeSize > 0)
                {
                    jpegStream.Read(Current.Array, Current.DataOffset, CodeSize - Marker.LengthBytes);

                    streamOffset += CodeSize;
                }

                yield return Current;

                CodeSize = 0;
            }
        }

        internal const int BlockSize = 8;
        internal static readonly double SqrtHalf = 1.0 / System.Math.Sqrt(2.0);

        private static void InverseQuantize(short[] block, short[] quantizationTable)
        {
            for (int i = 0; i < block.Length; i++)
            {
                block[i] *= quantizationTable[i];
            }
        }

        //Decompress

        internal static void VIDCT(Span<short> block, Span<double> output)
        {
            const int BlockSize = 8;
            double SqrtHalf = 1.0 / System.Math.Sqrt(2.0);
            Vector<double> sqrtHalfVector = new Vector<double>(SqrtHalf);
            Vector<double> quarterVector = new Vector<double>(0.25);

            for (int y = 0; y < BlockSize; y++)
            {
                for (int x = 0; x < BlockSize; x++)
                {
                    Vector<double> sumVector = Vector<double>.Zero;

                    for (int v = 0; v < BlockSize; v++)
                    {
                        for (int u = 0; u < BlockSize; u++)
                        {
                            double cu = (u == 0) ? SqrtHalf : 1.0;
                            double cv = (v == 0) ? SqrtHalf : 1.0;
                            double dctCoefficient = block[v * BlockSize + u];
                            double cosX = System.Math.Cos((2 * x + 1) * u * System.Math.PI / 16);
                            double cosY = System.Math.Cos((2 * y + 1) * v * System.Math.PI / 16);

                            Vector<double> cuVector = new Vector<double>(cu);
                            Vector<double> cvVector = new Vector<double>(cv);
                            Vector<double> dctCoefficientVector = new Vector<double>(dctCoefficient);
                            Vector<double> cosXVector = new Vector<double>(cosX);
                            Vector<double> cosYVector = new Vector<double>(cosY);

                            sumVector += cuVector * cvVector * dctCoefficientVector * cosXVector * cosYVector;
                        }
                    }

                    output[y * BlockSize + x] = Vector.Dot(quarterVector, sumVector);
                }
            }
        }

        internal static void IDCT(Span<short> block, Span<double> output)
        {
            for (int y = 0; y < BlockSize; y++)
            {
                for (int x = 0; x < BlockSize; x++)
                {
                    double sum = 0.0;

                    for (int v = 0; v < BlockSize; v++)
                    {
                        for (int u = 0; u < BlockSize; u++)
                        {
                            double cu = (u == 0) ? SqrtHalf : 1.0;
                            double cv = (v == 0) ? SqrtHalf : 1.0;
                            double dctCoefficient = block[v * BlockSize + u];
                            sum += cu * cv * dctCoefficient *
                                   System.Math.Cos((2 * x + 1) * u * System.Math.PI / 16) *
                                   System.Math.Cos((2 * y + 1) * v * System.Math.PI / 16);
                        }
                    }

                    output[y * BlockSize + x] = 0.25 * sum;
                }
            }
        }

        //Compress

        internal static void FDCT(Span<double> input, Span<double> output)
        {
            for (int u = 0; u < BlockSize; u++)
            {
                for (int v = 0; v < BlockSize; v++)
                {
                    double sum = 0.0;
                    for (int x = 0; x < BlockSize; x++)
                    {
                        for (int y = 0; y < BlockSize; y++)
                        {
                            sum += input[y * BlockSize + x] *
                                   System.Math.Cos((2 * x + 1) * u * System.Math.PI / 16) *
                                   System.Math.Cos((2 * y + 1) * v * System.Math.PI / 16);
                        }
                    }
                    double cu = (u == 0) ? SqrtHalf : 1.0;
                    double cv = (v == 0) ? SqrtHalf : 1.0;
                    output[v * BlockSize + u] = 0.25 * cu * cv * sum;
                }
            }
        }

        internal static void VFDCT(Span<double> input, Span<double> output)
        {
            for (int u = 0; u < BlockSize; u++)
            {
                for (int v = 0; v < BlockSize; v++)
                {
                    Vector<double> sum = Vector<double>.Zero;
                    for (int x = 0; x < BlockSize; x++)
                    {
                        for (int y = 0; y < BlockSize; y += Vector<double>.Count)
                        {
                            var inputVector = new Vector<double>(input);
                            var cosX = new Vector<double>(System.Math.Cos((2 * x + 1) * u * System.Math.PI / 16));
                            var cosY = new Vector<double>(System.Math.Cos((2 * y + 1) * v * System.Math.PI / 16));
                            sum += inputVector * cosX * cosY;
                        }
                    }
                    double cu = (u == 0) ? SqrtHalf : 1.0;
                    double cv = (v == 0) ? SqrtHalf : 1.0;
                    output[v * BlockSize + u] = 0.25 * cu * cv * Vector.Dot(sum, Vector<double>.One);
                }
            }
        }

        internal static void HuffmanEncode(Span<short> block, HuffmanTable dcTable, HuffmanTable acTable, BitWriter writer)
        {
            // DC coefficient encoding
            int dcValue = block[0];
            var (dcCode, dcLength) = dcTable.GetCode(dcValue);
            writer.WriteBits(dcCode, dcLength);

            // AC coefficients encoding
            int zeroCount = 0;
            for (int i = 1; i < block.Length; i++)
            {
                if (block[i] == 0)
                {
                    zeroCount++;
                }
                else
                {
                    while (zeroCount > 15)
                    {
                        // Write the special code for 16 zeros in a row
                        var (acCode, acLength) = acTable.GetCode(0xF0);
                        writer.WriteBits(acCode, acLength);
                        zeroCount -= 16;
                    }

                    // Encode the non-zero coefficient
                    int acValue = block[i];
                    int size = GetBitSize(acValue);
                    int acCodeValue = (zeroCount << 4) | size;
                    var ac = acTable.GetCode(acCodeValue);
                    writer.WriteBits(ac.code, ac.length);
                    writer.WriteBits(acValue, size);

                    zeroCount = 0;
                }
            }

            // Write the end-of-block (EOB) code if there are trailing zeros
            if (zeroCount > 0)
            {
                var (eobCode, eobLength) = acTable.GetCode(0x00);
                writer.WriteBits(eobCode, eobLength);
            }
        }

        private static int GetBitSize(int value)
        {
            int absValue = Binary.Abs(value);
            int size = 0;
            while (absValue > 0)
            {
                absValue >>= 1;
                size++;
            }
            return size;
        }

        internal static void VQuantize(Span<double> block, Span<short> quantizationTable, Span<short> output)
        {
            int VectorSize = Vector<double>.Count;
            int i = 0;

            // Process in chunks of VectorSize
            for (; i <= block.Length - VectorSize; i += VectorSize)
            {
                var blockVector = new Vector<double>(block);
                var quantizationVector = new Vector<double>(MemoryMarshal.Cast<short, byte>(quantizationTable));
                var resultVector = blockVector / quantizationVector;

                // Convert to short and store in output
                for (int j = 0; j < VectorSize; j++)
                {
                    output[i + j] = (short)System.Math.Round(resultVector[j]);
                }
            }

            // Process remaining elements
            for (; i < block.Length; i++)
            {
                output[i] = (short)System.Math.Round(block[i] / quantizationTable[i]);
            }
        }

        internal static void Quantize(Span<double> block, Span<short> quantizationTable, Span<short> output)
        {
            for (int i = 0; i < BlockSize * BlockSize; i++)
            {
                output[i] = (short)System.Math.Round(block[i] / quantizationTable[i]);
            }
        }

        private static int DecodeHuffman(BitReader stream, HuffmanTable table)
        {
            int code = 0;
            int length = 0;

            for (int i = 0; i < table.MaxCode.Length; i++)
            {
                code = (code << 1) | (stream.ReadBit() ? Binary.One : Binary.Zero);
                length++;

                if (code <= table.MaxCode[i])
                {
                    int index = table.ValPtr[i] + (code - table.MinCode[i]);
                    if (index < table.Values.Length)
                    {
                        return table.Values[index];
                    }
                }
            }

            return 0;
        }

        public static void Decompress(BitReader stream)
        {
            // Todo, these should be passed out to the caller so they can be installed (copied to the resulting image)
            // Example Huffman tables (you need to initialize these properly)
            HuffmanTable dcTable = new HuffmanTable() { Id = 0 };
            HuffmanTable acTable = new HuffmanTable() { Id = 1 };

            // Decode DC and AC values
            int dcValue = DecodeHuffman(stream, dcTable);
            int acValue = DecodeHuffman(stream, acTable);

            // Example block and quantization table
            short[] block = new short[BlockSize * BlockSize];
            short[] quantizationTable = new short[BlockSize * BlockSize];

            // Perform inverse quantization
            InverseQuantize(block, quantizationTable);

            // Perform IDCT
            double[] output = new double[BlockSize * BlockSize];
            
            if (Vector.IsHardwareAccelerated)
                VIDCT(block, output);
            else
                IDCT(block, output);
        }

        public static void Compress(JpegImage jpegImage, Stream outputStream, int quality)
        {
            // Create a stream around the raw data and compress it to the stream
            using var inputStream = new MemoryStream(jpegImage.Data.Array, jpegImage.Data.Offset, jpegImage.Data.Count, true);
            using (var reader = new BitReader(inputStream))
            using (var writer = new BitWriter(outputStream))
            {
                var blockSizeSquared = BlockSize * BlockSize;

                // Span
                // Example quantization table (you need to initialize this properly)
                Span<short> quantizationTable = stackalloc short[blockSizeSquared];

                // Span
                // Read the input data (this part is simplified for illustration purposes)
                Span<double> inputBlock = stackalloc double[blockSizeSquared];

                for (int i = 0; i < BlockSize * BlockSize; i++)
                {
                    inputBlock[i] = reader.Read8();
                }

                // Span
                // Perform forward DCT and quantization
                Span<double> dctBlock = stackalloc double[blockSizeSquared];
                Span<short> quantizedBlock = stackalloc short[blockSizeSquared];

                if (Vector.IsHardwareAccelerated)
                {
                    VFDCT(inputBlock, dctBlock);
                    VQuantize(dctBlock, quantizationTable, quantizedBlock);
                }
                else
                {
                    FDCT(inputBlock, dctBlock);
                    Quantize(dctBlock, quantizationTable, quantizedBlock);
                }

                // Perform Huffman encoding
                HuffmanEncode(quantizedBlock, jpegImage.JpegState.DcTable, jpegImage.JpegState.AcTable, writer);
            }

            // Write the SOS marker
            WriteStartOfScan(jpegImage, outputStream);
        }

        internal static void WriteStartOfScan(JpegImage jpegImage, Stream stream)
        {
            var numberOfComponents = jpegImage.ImageFormat.Components.Length;

            using var sos = new StartOfScan(numberOfComponents);

            sos.Ss = jpegImage.JpegState.Ss;
            sos.Se = jpegImage.JpegState.Se;
            sos.Ah = jpegImage.JpegState.Ah;
            sos.Al = jpegImage.JpegState.Al;

            for (var i = 0; i < numberOfComponents; ++i)
            {
                var imageComponent = jpegImage.ImageFormat.Components[i];

                if (imageComponent is JpegComponent jpegComponent)
                {
                    var componentSelector = new ScanComponentSelector();
                    componentSelector.Csj = jpegComponent.Id;
                    componentSelector.Tdj = jpegComponent.Tdj;
                    componentSelector.Taj = jpegComponent.Taj;
                    sos[i] = componentSelector;
                }
                else
                {
                    var componentSelector = new ScanComponentSelector();
                    componentSelector.Csj = (byte)i;
                    componentSelector.Tdj = (byte)i;
                    componentSelector.Taj = (byte)i;
                    sos[i] = componentSelector;
                }
            }

            WriteMarker(stream, sos);
        }

        internal static void WriteStartOfFrame(JpegImage jpegImage, Stream stream)
        {
            var componentCount = jpegImage.ImageFormat.Components.Length;
            using StartOfFrame sof = new StartOfFrame(jpegImage.JpegState.StartOfFrameFunctionCode, componentCount);
            sof.P = Binary.Clamp(jpegImage.ImageFormat.Size, Binary.BitsPerByte, byte.MaxValue);
            sof.Y = jpegImage.Height;
            sof.X = jpegImage.Width;
            for (var i = 0; i < componentCount; ++i)
            {
                var imageComponent = jpegImage.ImageFormat.Components[i];

                if (imageComponent is JpegComponent jpegComponent)
                {
                    var frameComponent = new FrameComponent(jpegComponent.Id, jpegImage.ImageFormat.HorizontalSamplingFactors[i], jpegImage.ImageFormat.VerticalSamplingFactors[i], jpegComponent.Tqi);
                    sof[i] = frameComponent;
                }
                else
                {
                    var frameComponent = new FrameComponent(imageComponent.Id, jpegImage.ImageFormat.HorizontalSamplingFactors[i], jpegImage.ImageFormat.VerticalSamplingFactors[i], i);
                    sof[i] = frameComponent;
                }
            }
            JpegCodec.WriteMarker(stream, sof);
        }

        internal static void WriteInformationMarker(byte functionCode, Stream stream)
        {
            stream.WriteByte(Markers.Prefix);
            stream.WriteByte(functionCode);
        }

        internal static void WriteQuantizationTableMarker(Stream stream, int quality)
        {
            const int QuantizationTableLength = 64; // Each quantization table has 64 values

            // Calculate the quantization table based on the quality
            short[] quantizationTable = GetQuantizationTable(quality, QuantizationTableType.Luminance);

            var outputMarker = new Marker(Markers.QuantizationTable, Marker.LengthBytes + 1 + QuantizationTableLength);

            // Write the precision and identifier (assuming 8-bit precision and identifier 0)
            byte precisionAndIdentifier = 0; // 0 for 8-bit precision and identifier 0
            outputMarker.Array[outputMarker.DataOffset] = precisionAndIdentifier;

            var i = 1;

            // Write the quantization table values
            foreach (short value in quantizationTable)
            {
                outputMarker.Array[outputMarker.DataOffset + i ++] = (byte)value;
            }

            // Write the DQT marker
            WriteMarker(stream, outputMarker);

            outputMarker.Dispose();
            outputMarker = null;

            // Calculate the quantization table based on the quality
            quantizationTable = GetQuantizationTable(quality, QuantizationTableType.Luminance);

            outputMarker = new Marker(Markers.QuantizationTable, Marker.LengthBytes + 1 + QuantizationTableLength);

            // Write the precision and identifier (assuming 8-bit precision and identifier 0)
            precisionAndIdentifier = 1; // 0 for 8-bit precision and identifier 0
            outputMarker.Array[outputMarker.DataOffset] = precisionAndIdentifier;

            i = 1;

            // Write the quantization table values
            foreach (short value in quantizationTable)
            {
                outputMarker.Array[outputMarker.DataOffset + i++] = (byte)value;
            }

            // Write the DQT marker
            WriteMarker(stream, outputMarker);
            outputMarker.Dispose();
            outputMarker = null;
        }

        private static readonly short[] DefaultLuminanceQuantTable = new short[64]
    {
        16, 11, 10, 16, 24, 40, 51, 61,
        12, 12, 14, 19, 26, 58, 60, 55,
        14, 13, 16, 24, 40, 57, 69, 56,
        14, 17, 22, 29, 51, 87, 80, 62,
        18, 22, 37, 56, 68, 109, 103, 77,
        24, 35, 55, 64, 81, 104, 113, 92,
        49, 64, 78, 87, 103, 121, 120, 101,
        72, 92, 95, 98, 112, 100, 103, 99
    };

        private static readonly short[] DefaultChrominanceQuantTable = new short[64]
        {
        17, 18, 24, 47, 99, 99, 99, 99,
        18, 21, 26, 66, 99, 99, 99, 99,
        24, 26, 56, 99, 99, 99, 99, 99,
        47, 66, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99
        };

        internal enum QuantizationTableType
        {
            Luminance,
            Chrominance
        }

        internal static short[] GetQuantizationTable(int quality, QuantizationTableType tableType)
        {
            if (quality < 1 || quality > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(quality), "Quality must be between 1 and 100.");
            }

            short[] baseTable = tableType == QuantizationTableType.Luminance
                ? DefaultLuminanceQuantTable
                : DefaultChrominanceQuantTable;

            if (quality == 50)
            {
                return (short[])baseTable.Clone();
            }

            int scaleFactor = quality < 50 ? 5000 / quality : 200 - quality * 2;

            short[] quantizationTable = new short[64];
            for (int i = 0; i < 64; i++)
            {
                int value = (baseTable[i] * scaleFactor + 50) / 100;
                quantizationTable[i] = (short)Math.Clamp(value, 1, 255);
            }

            return quantizationTable;
        }

        internal static void WriteHuffmanTableMarkers(Stream stream, params HuffmanTable[] huffmanTables)
        {
            foreach (var huffmanTable in huffmanTables)
            {
                // Create a marker for the Huffman table
                Marker huffmanTableMarker = new Marker(Markers.HuffmanTable, Marker.LengthBytes + CalculateHuffmanTableSize(huffmanTable));

                //Todo make use of data offset.
                using var slice = huffmanTableMarker.Data;

                using var memoryStream = slice.ToMemoryStream();

                // Write the Huffman table data to the stream
                WriteHuffmanTableData(memoryStream, huffmanTable);

                // Write the marker to the stream
                WriteMarker(stream, huffmanTableMarker);
            }
        }

        private static void WriteHuffmanTableData(Stream stream, HuffmanTable huffmanTable)
        {
            // Write the table class and identifier (assuming 0 for DC and 1 for AC)
            //TODO
            byte tableClassAndId = 0; // Replace with actual logic to determine the table class and identifier
            stream.WriteByte(tableClassAndId);

            // Write the number of codes for each bit length (1 to 16)
            for (int i = 0; i < 16; i++)
            {
                byte codeLengthCount = (byte)huffmanTable.CodeTable.Count(kv => kv.Value.length == i + 1);
                stream.WriteByte(codeLengthCount);
            }

            // Write the values
            stream.Write(huffmanTable.Values, 0, huffmanTable.Values.Length);
        }

        private static int CalculateHuffmanTableSize(HuffmanTable huffmanTable)
        {
            // Calculate the size of the Huffman table based on its CodeTable
            // This is a placeholder implementation and should be replaced with actual logic
            return huffmanTable.CodeTable.Count * 2; // Example calculation
        }

        internal static void WriteMarker(Stream stream, Marker marker)
        {
            stream.Write(marker.Array, marker.Offset, marker.MarkerLength);
        }
    }
}