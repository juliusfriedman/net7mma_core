using Media.Common;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System;

namespace Media.Codec.Jpeg.Classes;

internal class BaselineScan : JpegScan
{
    internal static readonly double SqrtHalf = 1.0 / Math.Sqrt(2.0);

    public BaselineScan() : base()
    {
    }

    #region Decompress

    private static void InverseQuantize(Span<short> block, QuantizationTable quantizationTable)
    {
        using var qk = quantizationTable.Qk;

        if (quantizationTable.Pq > 0)
        {
            var span = MemoryMarshal.Cast<byte, short>(qk.ToSpan());
            for (int i = 0; i < block.Length; i++)
            {
                block[i] *= span[i];
            }
        }
        else
        {
            for (int i = 0; i < block.Length; i++)
            {
                block[i] *= qk[i];
            }
        }
    }

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

    internal void IDCT(Span<short> block, Span<double> output)
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

    private static int DecodeHuffman(BitReader stream, HuffmanTable table)
    {
        int code = 0;
        int length = 0;
        var codeSumLength = table.CodeLengthSum;

        // Read bits one by one and traverse the Huffman tree
        while (true)
        {
            // Read the next bit from the stream
            int bit = (int)stream.ReadBits(1);

            // Append the bit to the code
            code = (code << 1) | bit;
            length++;

            // Try to get the Huffman code from the table
            if (table.TryGetCode(code, out var value))
            {
                return value.length;
            }

            // Check for an invalid length (e.g., exceeding the maximum code length)
            if (length > table.CodeLengthSum)
            {
                throw new InvalidDataException("Invalid Huffman code length.");
            }
        }
    }

    private short[] ReadBlock(BitReader stream, HuffmanTable dcTable, HuffmanTable acTable, ref int previousDC)
    {
        //Todo, should not build every time called.
        dcTable.BuildCodeTable();
        acTable.BuildCodeTable();

        var block = new short[BlockSize * BlockSize];  // Assuming 8x8 block

        // Decode DC coefficient
        int dcDifference = DecodeHuffman(stream, dcTable);
        previousDC += dcDifference;
        block[0] = (short)previousDC;

        // Decode AC coefficients
        int i = 1;
        while (i < BlockSize * BlockSize)
        {
            int acValue = DecodeHuffman(stream, acTable);

            if (acValue == 0)  // End of Block (EOB)
                break;

            int runLength = (acValue >> 4) & 0xF;  // Upper 4 bits
            int coefficient = acValue & 0xF;       // Lower 4 bits

            i += runLength;  // Skip zeros
            if (i < BlockSize * BlockSize)
            {
                block[i] = (short)DecodeCoefficient(stream, coefficient);
                i++;
            }
        }

        return block;
    }

    private static int DecodeCoefficient(BitReader stream, int size)
    {
        if (size == 0)
            return 0;

        // Read 'size' number of bits
        int value = (int)stream.ReadBits(size);

        // Convert to a signed integer
        int signBit = 1 << (size - 1);
        if ((value & signBit) == 0)
        {
            // If the sign bit is not set, the number is negative
            value -= (1 << size) - 1;
        }

        return value;
    }

    public override void Decompress(JpegImage jpegImage)
    {
        using var bitReader = new BitReader(jpegImage.Data.Array, Binary.BitOrder.MostSignificant, 0, 0, true, Environment.ProcessorCount * Environment.ProcessorCount);

        foreach (JpegComponent component in jpegImage.ImageFormat.Components)
        {
            var dcTable = jpegImage.JpegState.GetHuffmanTable(0, component.Tdj);

            var acTable = jpegImage.JpegState.GetHuffmanTable(1, component.Taj);

            var quantTable = jpegImage.JpegState.GetQuantizationTable(component.Tqi);

            //Should have logging support
            if (dcTable == null || acTable == null || quantTable == null)
                continue;

            int blockWidth = (jpegImage.Width + 7) / 8;
            int blockHeight = (jpegImage.Height + 7) / 8;
            int previousDC = 0;
            for (int by = 0; by < blockHeight; by++)
            {
                for (int bx = 0; bx < blockWidth; bx++)
                {
                    var block = ReadBlock(bitReader, dcTable, acTable, ref previousDC);

                    // Step 4: Dequantize
                    InverseQuantize(block, quantTable);

                    // Step 5: Inverse DCT
                    var output = new double[BlockSize * BlockSize];
                    IDCT(block, output);

                    // Step 6: Reconstruct Image
                    PlaceBlockInImage(jpegImage, component, output, bx, by);
                }
            }
        }
    }

    private void PlaceBlockInImage(JpegImage JpegImage, JpegComponent component, double[] block, int blockX, int blockY)
    {
        int blockSize = BlockSize;
        int width = JpegImage.Width;
        int height = JpegImage.Height;

        var memory = new MemorySegment(2);

        for (int i = 0; i < blockSize; i++)
        {
            for (int j = 0; j < blockSize; j++)
            {
                int x = blockX * blockSize + j;
                int y = blockY * blockSize + i;

                Binary.Write16(memory.Array, memory.Offset, Binary.IsLittleEndian, (short)block[i * blockSize + j]);

                JpegImage.SetComponentData(x, y, component.Id, memory);
            }
        }
    }

    #endregion

    #region Compress        

    internal void FDCT(Span<short> input)
    {
        Span<double> temp = stackalloc double[BlockSize * BlockSize];

        // Convert input from short to double
        for (int i = 0; i < input.Length; i++)
        {
            temp[i] = input[i];
        }

        Span<double> output = stackalloc double[BlockSize * BlockSize];

        for (int u = 0; u < BlockSize; u++)
        {
            for (int v = 0; v < BlockSize; v++)
            {
                double sum = 0.0;
                for (int x = 0; x < BlockSize; x++)
                {
                    for (int y = 0; y < BlockSize; y++)
                    {
                        double inputVal = temp[y * BlockSize + x];
                        double cosX = Math.Cos((2 * x + 1) * u * Math.PI / 16);
                        double cosY = Math.Cos((2 * y + 1) * v * Math.PI / 16);
                        sum += inputVal * cosX * cosY;
                    }
                }
                double cu = (u == 0) ? SqrtHalf : 1.0;
                double cv = (v == 0) ? SqrtHalf : 1.0;
                output[v * BlockSize + u] = 0.25 * cu * cv * sum;
            }
        }

        // Convert output from double to short
        for (int i = 0; i < output.Length; i++)
        {
            input[i] = (short)Math.Round(output[i]);
        }
    }

    internal void VFDCT(Span<short> input)
    {
        Span<double> temp = stackalloc double[BlockSize * BlockSize];

        // Convert input from short to double
        for (int i = 0; i < input.Length; i++)
        {
            temp[i] = input[i];
        }

        Span<double> output = stackalloc double[BlockSize * BlockSize];

        for (int u = 0; u < BlockSize; u++)
        {
            for (int v = 0; v < BlockSize; v++)
            {
                double sum = 0.0;
                for (int x = 0; x < BlockSize; x++)
                {
                    for (int y = 0; y < BlockSize; y++)
                    {
                        double inputVal = temp[y * BlockSize + x];
                        double cosX = Math.Cos((2 * x + 1) * u * Math.PI / 16);
                        double cosY = Math.Cos((2 * y + 1) * v * Math.PI / 16);
                        sum += inputVal * cosX * cosY;
                    }
                }
                double cu = (u == 0) ? SqrtHalf : 1.0;
                double cv = (v == 0) ? SqrtHalf : 1.0;
                output[v * BlockSize + u] = 0.25 * cu * cv * sum;
            }
        }

        // Convert output from double to short
        for (int i = 0; i < output.Length; i++)
        {
            input[i] = (short)Math.Round(output[i]);
        }
    }

    internal static void HuffmanEncode(Span<short> block, BitWriter writer, JpegState jpegState)
    {
        // Assuming jpegState contains Huffman tables for DC and AC components
        var dcTable = jpegState.GetHuffmanTable(0, 0); // Example for DC Luminance
        var acTable = jpegState.GetHuffmanTable(1, 0); // Example for AC Luminance

        if (dcTable == null || acTable == null)
        {
            throw new InvalidOperationException("Huffman tables are not initialized.");
        }

        dcTable.BuildCodeTable();
        acTable.BuildCodeTable();

        // Encode DC coefficient
        short dcCoefficient = block[0];
        int dcCoefficientSize = GetCoefficientSize(dcCoefficient);
        var dcCode = dcTable.GetCode((byte)dcCoefficientSize);
        writer.WriteBits(dcCode.length, dcCode.code);
        if (dcCoefficientSize > 0)
        {
            writer.WriteBits(dcCoefficientSize, dcCoefficient);
        }

        // Encode AC coefficients
        int runLength = 0;
        for (int i = 1; i < block.Length; i++)
        {
            short acCoefficient = block[i];
            if (acCoefficient == 0)
            {
                runLength++;
            }
            else
            {
                while (runLength > 15)
                {
                    var zrlCode = acTable.GetCode(0xF0); // ZRL (Zero Run Length) code
                    writer.WriteBits(zrlCode.length, zrlCode.code);
                    runLength -= 16;
                }

                int acCoefficientSize = GetCoefficientSize(acCoefficient);
                int acCodeValue = (runLength << 4) | acCoefficientSize;
                if (acTable.TryGetCode((byte)acCodeValue, out var ac))
                {
                    writer.WriteBits(ac.length, ac.code);
                    writer.WriteBits(acCoefficientSize, acCoefficient);
                    runLength = 0;
                }
            }
        }

        // End of Block (EOB) code
        if (runLength > 0)
        {
            var eobCode = acTable.GetCode(0x00); // EOB code
            writer.WriteBits(eobCode.length, eobCode.code);
        }

        // Ensure the BitWriter is byte-aligned after encoding
        writer.ByteAlign();

        // Ensure the BitWriter is flushed after encoding
        writer.Flush();
    }

    private static int GetCoefficientSize(short coefficient)
    {
        if (coefficient == 0) return 0;
        return Binary.Log2i(coefficient) + 1;
    }

    internal static void Quantize(Span<short> block, QuantizationTable quantizationTable)
    {
        // Process the block in chunks of Vector<short>.Count
        int vectorSize = Vector<short>.Count;
        var tableData = quantizationTable.GetTableData();
        for (int i = 0; i < block.Length; i += vectorSize)
        {
            // Load a chunk of the block and quantization table into vectors
            var blockVector = new Vector<short>(block.Slice(i, vectorSize));
            var quantVector = new Vector<short>(tableData);

            // Perform the quantization
            var resultVector = Vector.Divide(blockVector, quantVector);

            // Store the result back into the block
            resultVector.CopyTo(block.Slice(i, vectorSize));
        }
    }

    public override void Compress(JpegImage jpegImage, Stream outputStream)
    {
        // Create a stream around the raw data and compress it to the stream
        using var inputStream = new MemoryStream(jpegImage.Data.Array, jpegImage.Data.Offset, jpegImage.Data.Count, true);
        using var reader = new BitReader(inputStream, Environment.ProcessorCount * Environment.ProcessorCount);
        using var writer = new BitWriter(outputStream, Environment.ProcessorCount * Environment.ProcessorCount);

        for (var i = 0; i < jpegImage.ImageFormat.Components.Length; ++i)
        {
            var mediaComponent = jpegImage.ImageFormat.Components[i];

            if (mediaComponent is not JpegComponent)
            {
                mediaComponent = new JpegComponent((byte)(i == 0 ? 0 : 1), mediaComponent.Id, mediaComponent.Size);
                jpegImage.ImageFormat.Components[i] = mediaComponent;
            }
        }

        // Step 3: Block Splitting
        var blocks = SplitIntoBlocks(jpegImage);

        // Step 4: Discrete Cosine Transform (DCT)
        foreach (var block in blocks)
        {
            // Determine the quantization table to use based on the component
            var quantizationTable = jpegImage.JpegState.GetQuantizationTable(block.component.Tqi);

            if (quantizationTable == null)
                continue;

            // Perform the DCT
            if (Vector.IsHardwareAccelerated)
            {
                VFDCT(block.data);
            }
            else
            {
                FDCT(block.data);
            }

            // Step 5: Quantization
            Quantize(block.data, quantizationTable);

            // Step 6: Huffman Encoding
            HuffmanEncode(block.data, writer, jpegImage.JpegState);
        }
    }

    private List<(short[] data, JpegComponent component)> SplitIntoBlocks(JpegImage jpegImage)
    {
        int width = jpegImage.Width;
        int height = jpegImage.Height;
        var blocks = new List<(short[] block, JpegComponent component)>();

        for (int componentIndex = 0; componentIndex < jpegImage.ImageFormat.Components.Length; componentIndex++)
        {
            var mediaComponent = jpegImage.ImageFormat.Components[componentIndex] as JpegComponent;

            var componentHeight = jpegImage.PlaneHeight(componentIndex);

            var componentWidth = jpegImage.PlaneWidth(componentIndex);

            for (int y = 0; y < componentHeight; y += BlockSize)
            {
                for (int x = 0; x < componentWidth; x += BlockSize)
                {
                    short[] block = new short[BlockSize * BlockSize];
                    for (int by = 0; by < BlockSize; by++)
                    {
                        for (int bx = 0; bx < BlockSize; bx++)
                        {
                            int pixelX = x + bx;
                            int pixelY = y + by;
                            if (pixelX < componentWidth && pixelY < componentHeight)
                            {
                                block[by * BlockSize + bx] = jpegImage.GetComponentData(pixelX, pixelY, mediaComponent)[0];
                            }
                            else
                            {
                                block[by * BlockSize + bx] = 0; // Padding for blocks that go beyond image boundaries
                            }
                        }
                    }
                    blocks.Add((block, mediaComponent));
                }
            }
        }
        return blocks;
    }

    #endregion
}
