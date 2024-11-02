using Media.Common;
using System.IO;
using System.Numerics;
using System;
using Codec.Jpeg.Classes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Media.Codec.Jpeg.Segments;
using Media.Codecs.Image;

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

    #endregion

    #region Constructor

    public HuffmanScan() : base()
    {
    }

    #endregion

    #region Fields

    public int RestartInterval
    {
        get => restartInterval;
        set
        {
            restartInterval = todo = value;
        }
    }

    private int restartInterval;

    /// <summary>
    /// Emitted bits 'micro buffer' before being transferred to the <see cref="emitBuffer"/>.
    /// </summary>
    private uint accumulatedBits;

    /// <summary>
    /// Number of jagged bits stored in <see cref="accumulatedBits"/>
    /// </summary>
    private int bitCount;

    /// <summary>
    /// Amount of restart intervals remaining
    /// </summary>
    private int todo;

    #endregion

    #region Decompress

    public override void Decompress(JpegImage jpegImage)
    {
        int mcu = 0;
        int mcusPerColumn = jpegImage.JpegState.McusPerColumn;
        int mcusPerLine = jpegImage.JpegState.McusPerLine;

        using var bitReader = new BitReader(jpegImage.Data.Array, Binary.BitOrder.MostSignificant, 0, 0, true, Environment.ProcessorCount * Environment.ProcessorCount);

        var scanData = jpegImage.JpegState.ScanBuffer.ToSpan();        

        for (int j = 0; j < mcusPerColumn; j++)
        {
            // decode from binary to spectral
            for (int i = 0; i < mcusPerLine; i++)
            {
                for (int k = 0; k < jpegImage.ImageFormat.Components.Length; k++)
                {
                    var component = jpegImage.ImageFormat.Components[k] as Component;

                    if (component == null)
                        continue;

                    var dcTable = jpegImage.JpegState.GetHuffmanTable(0, component.Tdj);

                    var acTable = jpegImage.JpegState.GetHuffmanTable(1, component.Taj);

                    if (dcTable == null || acTable == null)
                        continue;

                    var dcLookupTable = new HuffmanLookupTable(dcTable);
                    var acLookupTable = new HuffmanLookupTable(acTable);

                    var dequantizationTable = jpegImage.JpegState.GetQuantizationTable(component.Tqi);

                    if (dequantizationTable == null)
                        continue;

                    var dequantizationBlock = dequantizationTable.AsBlock();                    

                    DiscreteCosineTransformation.AdjustToIDCT(dequantizationBlock);

                    int h = component.HorizontalSamplingFactor;
                    int v = component.VerticalSamplingFactor;

                    var blockAreaSize = component.SubSamplingDivisors * BlockSize;

                    //var blocksPerLine = (int)Math.Ceiling(Math.Ceiling((double)jpegImage.Width / BlockSize) *
                                        //h / jpegImage.JpegState.MaximumHorizontalSamplingFactor);

                    // Scan out an mcu's worth of this component; that's just determined
                    // by the basic H and V specified for the component
                    for (int y = 0; y < v; y++)
                    {
                        using (var block = new Block())
                        {
                            for (int x = 0; x < h; x++)
                            {
                                DecodeBlockBaseline(
                                    component,
                                    block,
                                    dcLookupTable,
                                    acLookupTable,
                                    bitReader);

                                int blocksRowsPerStep = component.SamplingFactors!.Height;

                                // Dequantize
                                block.MultiplyInPlace(dequantizationBlock);

                                // Convert from spectral to color
                                DiscreteCosineTransformation.TransformIDCT(block);

                                // To conform better to libjpeg we actually NEED TO loose precision here.
                                // This is because they store blocks as Int16 between all the operations.
                                // To be "more accurate", we need to emulate this by rounding!
                                block.NormalizeColorsAndRoundInPlace(jpegImage.JpegState.MaxColorChannelValue);

                                // Write to scan buffer acording to sampling factors
                                int xColorBufferStart = x * blockAreaSize.Width;

                                var floatSpan = MemoryMarshal.Cast<byte, float>(scanData);

                                block.ScaledCopyTo(
                                    ref floatSpan[xColorBufferStart],
                                    jpegImage.Width,
                                    component.SubSamplingDivisors!.Width,
                                    component.SubSamplingDivisors!.Height);

                                scanData = scanData.Slice(block.FloatLength);
                            }
                        }
                    }
                }

                // After all interleaved components, that's an interleaved MCU,
                // so now count down the restart interval
                mcu++;
                HandleRestart(jpegImage, bitReader);
            }           
        }

        //scanData and jpegImage.Data need to be swapped so the pixel accessors work.
        //This indicates there should be a CompressedImage class.
        //Compress would return a CompressedImage and Decompress would return an Image.
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HandleRestart(JpegImage jpegImage, BitReader reader)
    {
        var markerPosition = reader.BaseStream.Position;
        Marker? marker = null;
        if (restartInterval > 0 && (--todo) == 0)
        {
            if (reader.Buffer[reader.ByteIndex] == Markers.Prefix)
            {
                marker = new Marker(reader.Cache.Slice(reader.ByteIndex));
                if (!Markers.IsKnownFunctionCode(marker.FunctionCode))
                {
                    return false;
                }
            }

            todo = restartInterval;

            if (marker != null && Markers.IsRestartMarker(marker.FunctionCode))
            {
                Reset(jpegImage);
                return true;
            }

            if (marker != null && !Markers.IsKnownFunctionCode(marker.FunctionCode))
            {
                reader.BaseStream.Position = markerPosition;
                Reset(jpegImage);
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Reset(JpegImage jpegImage)
    {
        for (int i = 0; i < jpegImage.ImageFormat.Components.Length; i++)
        {
            var jpegComponent = jpegImage.ImageFormat.Components[i] as Component;

            if (jpegComponent == null)
                continue;

            jpegComponent.DcPredictor = 0;
        }

        //Used in progressive mode.
        //this.eobrun = 0;
    }

    private void DecodeBlockBaseline(
        Component component,
        Block block,
        HuffmanLookupTable dcTable,
        HuffmanLookupTable acTable,
        BitReader reader)
    {
        var span = block.ToSpan();

        var shortSpan = MemoryMarshal.Cast<byte, short>(span);

        ref short blockDataRef = ref shortSpan[0];

        // DC
        int t = DecodeHuffman(reader, dcTable);
        if (t != 0)
        {
            t = Receive(t, reader);
        }

        t += component.DcPredictor;
        component.DcPredictor = t;
        blockDataRef = (short)t;

        // AC
        for (int i = 1; i < 64;)
        {
            int s = DecodeHuffman(reader, acTable);

            int r = s >> 4;
            s &= 15;

            if (s != 0)
            {
                i += r;
                s = Receive(s, reader);
                Unsafe.Add(ref blockDataRef, ZigZag.TransposingOrder[i++]) = (short)s;
            }
            else
            {
                if (r == 0)
                {
                    break;
                }

                i += 16;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Receive(int nbits, BitReader reader)
    {
        return Extend((int)reader.ReadBits(nbits), nbits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Extend(int v, int nbits) => v - ((((v + v) >> nbits) - 1) & ((1 << nbits) - 1));

    private static int DecodeHuffman(BitReader bitReader, HuffmanLookupTable table)
    {
        try
        {
            ulong index = bitReader.ReadBits(LookupBits);
            int size = table.LookaheadSize[index];

            if (size < SlowBits)
            {
                return table.LookaheadValue[index];
            }

            ulong x = index << (RegisterSize - bitReader.BufferBitsRemaining);
            while (x > table.MaxCode[size])
            {
                size++;
            }

            return table.Values[(table.ValOffset[size] + (int)(x >> (RegisterSize - size))) & byte.MaxValue];
        }
        catch(EndOfStreamException)
        {
            return 0;
        }
    }

    #endregion

    #region Compress        

    public override void Compress(JpegImage jpegImage, Stream outputStream)
    {
        using var bitWriter = new BitWriter(outputStream, Environment.ProcessorCount * Environment.ProcessorCount);

        int restarts = 0;
        int restartsToGo = RestartInterval;
        
        //Should be ScanBuffer
        var span = jpegImage.Data.ToSpan();

        var floatData = MemoryMarshal.Cast<byte, float>(span);

        for (var c = 0; c < jpegImage.ImageFormat.Components.Length; c++)
        {
            var component = jpegImage.ImageFormat.Components[c] as Component;

            if (component == null)
                continue;

            int h = component.HeightInBlocks;
            int w = component.WidthInBlocks;

            var dcTable = jpegImage.JpegState.GetHuffmanTable(0, component.Tdj);

            var acTable = jpegImage.JpegState.GetHuffmanTable(1, component.Taj);

            if (dcTable == null || acTable == null)
                continue;

            var dcLookupTable = new HuffmanLookupTable(dcTable);

            var acLookupTable = new HuffmanLookupTable(acTable);

            var quantTable = jpegImage.JpegState.GetQuantizationTable(component.Tqi);

            if (quantTable == null)
                continue;

            using var quantBlock = quantTable.AsBlock();

            using var block = new Block();                        

            for (int i = 0; i < h; i++)
            {
                for (nuint k = 0; k < (uint)w; k++)
                {
                    // load 8x8 block from 8 pixel strides
                    int xBufferStart = i * (int)k * jpegImage.ImageFormat.Length * JpegCodec.BlockSize;
                    block.ScaledCopyFrom(
                        ref floatData[xBufferStart],
                        w);

                    // level shift via -128f
                    block.AddInPlace(-128f);

                    // FDCT
                    DiscreteCosineTransformation.TransformFDCT(block);

                    // Quantize and save to spectral blocks
                    Block.Quantize(block, block, quantBlock);

                    if (RestartInterval > 0 && restartsToGo == 0)
                    {
                        WriteRestart(restarts % 8, bitWriter.BaseStream);
                        component.DcPredictor = 0;
                    }

                    WriteBlock(
                        component,
                        block,
                        dcTable,
                        acTable,
                        bitWriter);

                    if (RestartInterval > 0)
                    {
                        if (restartsToGo == 0)
                        {
                            restartsToGo = RestartInterval;
                            restarts++;
                        }

                        restartsToGo--;
                    }
                }
            }
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
        ref short blockRef = ref Unsafe.As<byte, short>(ref block.GetReference(0));
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
        accumulatedBits |= bits >> bitCount;
        count += bitCount;
        if (count >= Binary.BitsPerInteger)
        {
            output.WriteBits(Binary.BitsPerInteger, accumulatedBits);
            accumulatedBits = bits << (Binary.BitsPerInteger - bitCount);
            count -= Binary.BitsPerInteger;
        }
        bitCount = count;
    }

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
}
