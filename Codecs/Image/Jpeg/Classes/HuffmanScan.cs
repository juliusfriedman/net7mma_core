using Media.Common;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System;
using Codec.Jpeg.Classes;
using Codec.Jpeg.Segments;
using System.Runtime.CompilerServices;

namespace Media.Codec.Jpeg.Classes;

internal class HuffmanScan : Scan
{
    #region Constants

    /// <summary>
    /// Multiplier used within cache buffers size calculation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Theoretically, <see cref="MaxBytesPerBlock"/> bytes buffer can fit
    /// exactly one minimal coding unit. In reality, coding blocks occupy much
    /// less space than the theoretical maximum - this can be exploited.
    /// If temporal buffer size is multiplied by at least 2, second half of
    /// the resulting buffer will be used as an overflow 'guard' if next
    /// block would occupy maximum number of bytes. While first half may fit
    /// many blocks before needing to flush.
    /// </para>
    /// <para>
    /// This is subject to change. This can be equal to 1 but recomended
    /// value is 2 or even greater - futher benchmarking needed.
    /// </para>
    /// </remarks>
    private const int MaxBytesPerBlockMultiplier = 2;

    /// <summary>
    /// <see cref="streamWriteBuffer"/> size multiplier.
    /// </summary>
    /// <remarks>
    /// Jpeg specification requiers to insert 'stuff' bytes after each
    /// 0xff byte value. Worst case scenarion is when all bytes are 0xff.
    /// While it's highly unlikely (if not impossible) to get such
    /// combination, it's theoretically possible so buffer size must be guarded.
    /// </remarks>
    private const int OutputBufferLengthMultiplier = 2;

    /// <summary>
    /// Maximum number of bytes encoded jpeg 8x8 block can occupy.
    /// It's highly unlikely for block to occupy this much space - it's a theoretical limit.
    /// </summary>
    /// <remarks>
    /// Where 16 is maximum huffman code binary length according to itu
    /// specs. 10 is maximum value binary length, value comes from discrete
    /// cosine tranform with value range: [-1024..1023]. Block stores
    /// 8x8 = 64 values thus multiplication by 64. Then divided by 8 to get
    /// the number of bytes. This value is then multiplied by
    /// <see cref="MaxBytesPerBlockMultiplier"/> for performance reasons.
    /// </remarks>
    private const int MaxBytesPerBlock = (16 + 10) * 64 / JpegCodec.BlockSize * MaxBytesPerBlockMultiplier;

    #endregion

    #region Statics

    internal static readonly double SqrtHalf = 1.0 / Math.Sqrt(2.0);

    /// <summary>
    /// Calculates how many minimum bits needed to store given value for Huffman jpeg encoding.
    /// </summary>
    /// <remarks>
    /// This is an internal operation supposed to be used only in <see cref="HuffmanScanEncoder"/> class for jpeg encoding.
    /// </remarks>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetHuffmanEncodingLength(uint value)
    {
        // This should have been implemented as (BitOperations.Log2(value) + 1) as in non-intrinsic implementation
        // But internal log2 is implemented like this: (31 - (int)Lzcnt.LeadingZeroCount(value))

        // BitOperations.Log2 implementation also checks if input value is zero for the convention 0->0
        // Lzcnt would return 32 for input value of 0 - no need to check that with branching
        // Fallback code if Lzcnt is not supported still use if-check
        // But most modern CPUs support this instruction so this should not be a problem
        return 32 - BitOperations.LeadingZeroCount(value);
    }

    #endregion

    public HuffmanScan() : base()
    {
    }

    #region Fields

    public int RestartInterval;

    #endregion

    #region Decompress

    public override void Decompress(JpegImage jpegImage)
    {
        using var bitReader = new BitReader(jpegImage.Data.Array, Binary.BitOrder.MostSignificant, 0, 0, true, Environment.ProcessorCount * Environment.ProcessorCount);

        foreach (Component component in jpegImage.ImageFormat.Components)
        {
            var dcTable = jpegImage.JpegState.GetHuffmanTable(0, component.Tdj);

            var acTable = jpegImage.JpegState.GetHuffmanTable(1, component.Taj);

            var quantTable = jpegImage.JpegState.GetQuantizationTable(component.Tqi);

            //Should have logging support
            if (dcTable == null || acTable == null || quantTable == null)
                continue;

            int blockWidth = (jpegImage.Width + 7) / BlockSize;
            int blockHeight = (jpegImage.Height + 7) / BlockSize;
            int previousDC = 0;
            for (int by = 0; by < blockHeight; by++)
            {
                for (int bx = 0; bx < blockWidth; bx++)
                {
                    using Block block = ReadBlock(bitReader, dcTable, acTable, ref previousDC);

                    // Step 4: Dequantize
                    //InverseQuantize(block, quantTable);

                    // Step 5: Inverse DCT
                    DiscreteCosineTransformation.TransformIDCT(block);

                    // Step 6: Reconstruct Image
                    //PlaceBlockInImage(jpegImage, component, block, bx, by);
                }
            }
        }
    }

    internal Block ReadBlock(BitReader bitReader, HuffmanTable dcTable, HuffmanTable acTable, ref int previousDC)
    {
        // Initialize a new block
        Block block = new Block();

        // Read the DC coefficient
        int dcCoefficientSize = DecodeHuffman(bitReader, dcTable);
        int dcCoefficient = (dcCoefficientSize == 0) ? 0 : (int)bitReader.ReadBitsSigned(dcCoefficientSize);
        dcCoefficient += previousDC;
        previousDC = dcCoefficient;
        block[0] = (short)dcCoefficient;

        // Read the AC coefficients
        int index = 1;
        while (index < BlockSize * BlockSize)
        {
            int acCoefficientSize = DecodeHuffman(bitReader, acTable);
            if (acCoefficientSize == 0)
            {
                // End of Block (EOB)
                break;
            }

            int runLength = acCoefficientSize >> 4;
            acCoefficientSize &= 0xF;

            index += runLength;
            if (index >= BlockSize * BlockSize)
            {
                break;
            }

            int acCoefficient = (acCoefficientSize == 0) ? 0 : (int)bitReader.ReadBitsSigned(acCoefficientSize);
            block[index] = (short)acCoefficient;
            index++;
        }

        return block;
    }

    private int DecodeHuffman(BitReader bitReader, HuffmanTable table)
    {
        int code = 0;
        int length = 0;

        while (true)
        {
            code = (code << 1) | (bitReader.ReadBit() ? 1 : 0);
            length++;

            if (table.TryGetCode((byte)code, out var huffmanCode) && huffmanCode.length == length)
            {
                return huffmanCode.code;
            }
        }
    }

    #endregion

    #region Compress        

    public override void Compress(JpegImage jpegImage, Stream outputStream)
    {
        // Create a stream around the raw data and compress it to the stream
        using var inputStream = new MemoryStream(jpegImage.Data.Array, jpegImage.Data.Offset, jpegImage.Data.Count, true);
        using var reader = new BitReader(inputStream, Environment.ProcessorCount * Environment.ProcessorCount);
        using var writer = new BitWriter(outputStream, Environment.ProcessorCount * Environment.ProcessorCount);

        var imageData = jpegImage.Data;

        var offset = 0;

        for (var i = 0; i < jpegImage.ImageFormat.Components.Length; ++i)
        {
            var mediaComponent = jpegImage.ImageFormat.Components[i];

            var component = mediaComponent as Component;

            if (component is null)
            {
                var jpegComponent = new Component((byte)(i == 0 ? 0 : 1), mediaComponent.Id, mediaComponent.Size);
                mediaComponent = jpegComponent;
                jpegImage.ImageFormat.Components[i] = mediaComponent;
                component = jpegComponent;
            }

            var acTable = jpegImage.JpegState.GetHuffmanTable(0, component.Taj);

            var dcTable = jpegImage.JpegState.GetHuffmanTable(1, component.Taj);

            if (acTable == null || dcTable == null)
                continue;

            using var slice = imageData.Slice(offset, Block.DefaultSize);

            using var block = new Block(slice);

            WriteBlock(component, block, dcTable, acTable, writer);
        }
    }

    private void WriteBlock(
        Component component,
        Block block,
        HuffmanTable dcTable,
        HuffmanTable acTable,
        BitWriter writer)
    {
        WriteDc(component, block, dcTable, writer);
        WriteAcBlock(block, 1, BlockSize * BlockSize, acTable, writer);
    }

    private void WriteRestart(int restartInterval, Stream output)
    {
        using var dri = new RestartInterval(restartInterval);
        JpegCodec.WriteMarker(output, dri);
    }

    private void WriteDc(
       Component component,
       Block block,
       HuffmanTable dcTable,
       BitWriter writer)
    {
        // Emit the DC delta.
        int dc = block[0];
        EmitHuffRLE(dcTable, 0, dc - component.DcPredictor, writer);
        component.DcPredictor = dc;
    }

    private void WriteAcBlock(
        Block block,
        nint start,
        nint end,
        HuffmanTable acTable,
        BitWriter writer)
    {

        int runLength = 0;
        ref short blockRef = ref Unsafe.As<byte, short>(ref block.Array[block.Offset]);
        for (nint zig = start; zig < end; zig++)
        {
            const int zeroRun1 = 1 << 4;
            const int zeroRun16 = 16 << 4;

            int ac = Unsafe.Add(ref blockRef, zig);
            if (ac == 0)
            {
                runLength += zeroRun1;
            }
            else
            {
                while (runLength >= zeroRun16)
                {
                    EmitHuff(acTable, 0xf0, writer);
                    runLength -= zeroRun16;
                }

                EmitHuffRLE(acTable, runLength, ac, writer);
                runLength = 0;
            }
        }

        // if mcu block contains trailing zeros - we must write end of block (EOB) value indicating that current block is over
        if (runLength > 0)
        {
            EmitHuff(acTable, 0x00, writer);
        }
    }

    /// <summary>
    /// Emits the given value with the given Huffman table.
    /// </summary>
    /// <param name="table">Huffman table.</param>
    /// <param name="value">Value to encode.</param>
    /// <param name="output">Output bit writer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitHuff(HuffmanTable table, int value, BitWriter output)
    {
        using var vi = table.Vi;
        int x = vi[value];
        Emit((uint)x & 0xffff_ff00u, x & 0xff, output);
    }

    private void EmitHuffRLE(HuffmanTable table, int runLength, int value, BitWriter writer)
    {
        int a = value;
        int b = value;
        if (a < 0)
        {
            a = -value;
            b = value - 1;
        }

        int valueLen = GetHuffmanEncodingLength((uint)a);

        // Huffman prefix code
        int huffPackage = table[runLength | valueLen];
        int prefixLen = huffPackage & 0xff;
        uint prefix = (uint)huffPackage & 0xffff_0000u;

        // Actual encoded value
        uint encodedValue = (uint)b << (32 - valueLen);

        // Doing two binary shifts to get rid of leading 1's in negative value case
        Emit(prefix | (encodedValue >> prefixLen), prefixLen + valueLen, writer);
    }

    /// <summary>
    /// Emits the most significant count of bits to the buffer.
    /// </summary>
    /// <param name="bits"></param>
    /// <param name="count"></param>
    /// <param name="output"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Emit(uint bits, int count, BitWriter output)
    {
        output.WriteBits(count, bits);
    }

    #endregion
}
