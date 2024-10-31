using Media.Common;
using System.IO;
using System.Numerics;
using System;
using Codec.Jpeg.Classes;
using Codec.Jpeg.Segments;
using System.Runtime.CompilerServices;

namespace Media.Codec.Jpeg.Classes;

internal class HuffmanScan : Scan
{
    #region Constants

    /// <summary>
    /// The size of the huffman decoder register.
    /// </summary>
    public const int RegisterSize = 64;

    /// <summary>
    /// The number of bits to fetch when filling the <see cref="JpegBitReader"/> buffer.
    /// </summary>
    public const int FetchBits = 48;

    /// <summary>
    /// The number of times to read the input stream when filling the <see cref="JpegBitReader"/> buffer.
    /// </summary>
    public const int FetchLoop = FetchBits / Binary.BitsPerByte;

    /// <summary>
    /// The minimum number of bits allowed before by the <see cref="JpegBitReader"/> before fetching.
    /// </summary>
    public const int MinBits = RegisterSize - FetchBits;

    /// <summary>
    /// If the next Huffman code is no more than this number of bits, we can obtain its length
    /// and the corresponding symbol directly from the tables.
    /// </summary>
    public const int LookupBits = Binary.BitsPerByte;

    /// <summary>
    /// If a Huffman code is this number of bits we cannot use the lookup table to determine its value.
    /// </summary>
    public const int SlowBits = LookupBits + 1;

    /// <summary>
    /// The size of the lookup table.
    /// </summary>
    public const int LookupSize = 1 << LookupBits;

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

                    using var quantBlock = quantTable.AsBlock();

                    // Step 4: Dequantize
                    DiscreteCosineTransformation.AdjustToIDCT(quantBlock);

                    // Step 5: Inverse DCT
                    DiscreteCosineTransformation.TransformIDCT(block);

                    // Step 6: Reconstruct Image
                    PlaceBlockInImage(jpegImage, component, block, bx, by);
                }
            }
        }
    }

    private void PlaceBlockInImage(JpegImage jpegImage, Component component, Block block, int bx, int by)
    {
        // Calculate the starting position in the image
        int startX = bx * block.FloatLength;
        int startY = by * block.FloatLength;

        // Copy the block data into the image's pixel data
        for (int y = 0; y < BlockSize; y++)
        {
            for (int x = 0; x < BlockSize; x++)
            {
                int imageX = bx * BlockSize + x;
                int imageY = by * BlockSize + y;

                // Check if the calculated coordinates are within the image bounds
                if (imageX >= 0 && imageX < jpegImage.Width && imageY >= 0 && imageY < jpegImage.Height)
                {
                    int pixelIndex = (startY + y) * jpegImage.Width + startX + x;
                    jpegImage.JpegState.ScanData![pixelIndex] = (byte)block[x, y];
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
        var lookupTable = new HuffmanLookupTable(table);

        ulong index = bitReader.ReadBits(LookupBits);
        int size = lookupTable.LookaheadSize[index];

        if (size < SlowBits)
        {
            return lookupTable.LookaheadValue[index];
        }

        ulong x = index << (RegisterSize - bitReader.BufferBitsRemaining);
        while (x > lookupTable.MaxCode[size])
        {
            size++;
        }

        return lookupTable.Values[(lookupTable.ValOffset[size] + (int)(x >> (RegisterSize - size))) & byte.MaxValue];
    }

    #endregion

    #region Compress        

    public override void Compress(JpegImage jpegImage, Stream outputStream)
    {
        using var writer = new BitWriter(outputStream, Environment.ProcessorCount * Environment.ProcessorCount);

        var imageData = jpegImage.JpegState.ScanData;

        var offset = 0;

        for (var i = 0; i < jpegImage.ImageFormat.Components.Length; ++i)
        {
            var mediaComponent = jpegImage.ImageFormat.Components[i];

            var component = mediaComponent as Component;

            if (component is null)
            {
                component = new Component((byte)(i == 0 ? 0 : 1), mediaComponent.Id, mediaComponent.Size);
                jpegImage.ImageFormat.Components[i] = component;
            }

            var acTable = jpegImage.JpegState.GetHuffmanTable(0, component.Taj);

            var dcTable = jpegImage.JpegState.GetHuffmanTable(1, component.Taj);

            if (acTable == null || dcTable == null)
                continue;

            using var slice = imageData.Slice(offset, Binary.Min(imageData.Count, Block.DefaultSize));

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
        WriteAcBlock(block, 1, 64, acTable, writer);
    }

    private static void WriteRestart(int restartInterval, Stream output)
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
        var lookupTable = new HuffmanLookupTable(dcTable);
        // Emit the DC delta.
        int dc = block[0];
        EmitHuffRLE(lookupTable, 0, dc - component.DcPredictor, writer);
        component.DcPredictor = dc;
    }

    private void WriteAcBlock(
        Block block,
        nint start,
        nint end,
        HuffmanTable acTable,
        BitWriter writer)
    {
        var lookupTable = new HuffmanLookupTable(acTable);
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
                    EmitHuff(lookupTable, 0xf0, writer);
                    runLength -= zeroRun16;
                }

                EmitHuffRLE(lookupTable, runLength, ac, writer);
                runLength = 0;
            }
        }

        // if mcu block contains trailing zeros - we must write end of block (EOB) value indicating that current block is over
        if (runLength > 0)
        {
            EmitHuff(lookupTable, 0x00, writer);
        }
    }

    /// <summary>
    /// Emits the given value with the given Huffman table.
    /// </summary>
    /// <param name="table">Huffman table.</param>
    /// <param name="value">Value to encode.</param>
    /// <param name="output">Output bit writer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitHuff(HuffmanLookupTable table, int value, BitWriter output)
    {
        int x = table.Values[value];
        Emit((uint)x & 0xffff_ff00u, x & 0xff, output);
    }

    private void EmitHuffRLE(HuffmanLookupTable table, int runLength, int value, BitWriter writer)
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
        int huffPackage = table.Values[runLength | valueLen];
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
