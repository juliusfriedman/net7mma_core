using Media.Codec;
using Media.Codec.Jpeg;
using Media.Codec.Jpeg.Classes;
using Media.Codec.Jpeg.Segments;
using Media.Codecs.Image;
using Media.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Media.UnitTests;

internal class JpegUnitTests
{
    public static void TestBlockSetGetVector4Properties()
    {
        var block = new Block();
        var vector = new Vector4(1, 2, 3, 4);
        block.V0L = vector;

        if (block.V0L != vector)
        {
            throw new Exception("Vector4 property set/get failed.");
        }

        for(int i = 0; i < 4; i++)
        {
            var value = block[(nuint)i];

            if(value != i + 1)
            {
                throw new Exception("Indexer is not aligned");
            }
        }

    }

    public static void TestBlockTotalDifference()
    {
        var block1 = new Block();
        var block2 = new Block();
        
        // Initialize block1 and block2 with some values
        for (int i = 0; i < Block.DefaultSize; i++)
        {
            block1[i] = (short)i;
            block2[i] = (short)(i + 1);
        }

        var difference = Block.TotalDifference(ref block1, ref block2);

        if (difference != Block.DefaultSize)
        {
            throw new Exception("TotalDifference calculation failed.");
        }

        block1 = new Block();
        block1.V0L = new Vector4(1, 2, 3, 4);
        block2.V0L = new Vector4(1, 2, 3, 5);

        difference = Block.TotalDifference(ref block1, ref block2);

        if (difference != 1)
        {
            throw new Exception("TotalDifference calculation failed.");
        }
    }

    public static void TestBlockLoad()
    {
        var data = new short[Block.DefaultSize];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (short)i;
        }

        var block = Block.Load(data);

        if (block == null)
        {
            throw new Exception("Block load failed.");
        }

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] != block[i])
                throw new Exception("Block indexer not aligned.");
        }

    }

    public static void TestBlockGetSpan()
    {
        var block = new Block();
        var vector = new Vector4(1, 2, 3, 4);
        block.V0L = vector;

        var span = block.GetFourFloats(0);

        if (block.V0L != vector ||
            span[0] != vector.X || span[1] != vector.Y || span[2] != vector.Z || span[3] != vector.W)
        {
            throw new Exception("GetSpan method failed.");
        }
    }

    public static void TestBlockAligment()
    {
        using var block = new Block();
        block.V0L = new Vector4(0, 1, 2, 3);
        block.V0R = new Vector4(4, 5, 6, 7);
        block.V1L = new Vector4(8, 9, 10, 11);
        block.V1R = new Vector4(12, 13, 14, 15);
        block.V2L = new Vector4(16, 17, 18, 19);
        block.V2R = new Vector4(20, 21, 22, 23);
        block.V3L = new Vector4(24, 25, 26, 27);
        block.V3R = new Vector4(28, 29, 30, 31);
        block.V4L = new Vector4(32, 33, 34, 35);
        block.V4R = new Vector4(36, 37, 38, 39);
        block.V5L = new Vector4(40, 41, 42, 43);
        block.V5R = new Vector4(44, 45, 46, 47);
        block.V6L = new Vector4(48, 49, 50, 51);
        block.V6R = new Vector4(52, 53, 54, 55);
        block.V7L = new Vector4(56, 57, 58, 59);
        block.V7R = new Vector4(60, 61, 62, 63);

        for (var i = 0; i < Block.DefaultSize; ++i)
        {
            var value = block[(uint)i];
            if (value != i)
            {
                throw new Exception("Not aligned.");
            }
        }
    }

    public static void TestSave()
    {
        var format = ImageFormat.RGB(8);
        using (var image = new JpegImage(format, 100, 100))
        {
            using (var stream = new MemoryStream())
            {
                image.Save(stream);
                stream.Position = 0;
                using (var jpgImage = JpegImage.FromStream(stream))
                {
                    if (jpgImage.Width != 100) throw new Exception();
                    if (jpgImage.Height != 100) throw new Exception();
                }
            }
        }

        var currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        var outputDirectory = Directory.CreateDirectory(Path.Combine(currentPath!, "Media", "JpegTest", "output"));

        foreach (var dataLayout in Enum.GetValues<DataLayout>())
        {
            if (dataLayout == DataLayout.Unknown) continue;

            using (var image = new JpegImage(Media.Codecs.Image.ImageFormat.RGB(8, Binary.ByteOrder.Little, dataLayout), 696, 564))
            {
                for (int i = 0; i < image.Width; i += 2)
                {
                    for (int j = 0; j < image.Height; j += 2)
                    {
                        for (int c = 0; c < image.ImageFormat.Components.Length; ++c)
                        {
                            var data = image.GetComponentData(i, j, image.ImageFormat[c]);

                            Array.Fill(data.Array, byte.MaxValue, data.Offset, data.Count);

                            image.SetComponentData(i, j, c, data);
                        }
                    }
                }

                using (var outputJpegStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"rgb_{dataLayout}.jpg"), FileMode.OpenOrCreate))
                {
                    image.Save(outputJpegStream);
                }

                using (var inputJpeg = JpegImage.FromStream(new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"rgb_{dataLayout}.jpg"), FileMode.OpenOrCreate)))
                {
                    if (inputJpeg.Width != 696) throw new Exception();

                    if (inputJpeg.Height != 564) throw new Exception();
                }
            }
        }

        foreach (var dataLayout in Enum.GetValues<DataLayout>())
        {
            if (dataLayout == DataLayout.Unknown) continue;

            using (var image = new JpegImage(Media.Codecs.Image.ImageFormat.RGB(8, Binary.SystemByteOrder, dataLayout), 696, 564))
            {
                for (int i = 0; i < image.Width; i += 2)
                {
                    for (int j = 0; j < image.Height; j += 2)
                    {
                        for (int c = 0; c < image.ImageFormat.Components.Length; ++c)
                        {
                            var data = image.GetComponentVector(i, j, c);

                            data = Vector<byte>.One * byte.MaxValue;

                            image.SetComponentVector(i, j, c, data);
                        }
                    }
                }

                using (var outputJpegStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"RGB_Vector_{dataLayout}.jpg"), FileMode.OpenOrCreate))
                {
                    image.Save(outputJpegStream);
                }
            }
        }

        using (var image = new JpegImage(ImageFormat.Binary(8), 696, 564))
        {
            for (int i = 0; i < image.Width; i += 4)
            {
                for (int j = 0; j < image.Height; j += 4)
                {
                    var data = image.GetVectorDataAt(i, j);

                    data = Vector<byte>.One * byte.MaxValue;

                    image.SetVectorDataAt(i, j, data);
                }
            }

            using (var outputJpegStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "Binary_packed_line.jpg"), FileMode.OpenOrCreate))
            {
                image.Save(outputJpegStream);
            }
        }

        using (var image = new JpegImage(ImageFormat.WithSubSampling(Media.Codecs.Image.ImageFormat.YUV(8), [2, 1, 1], [2, 1, 1]), 696, 564))
        {
            for (int i = 0; i < image.Width; i += 4)
            {
                for (int j = 0; j < image.Height; j += 4)
                {
                    var data = image.GetVectorDataAt(i, j);

                    data = Vector<byte>.One * byte.MaxValue;

                    image.SetVectorDataAt(i, j, data);
                }
            }

            using (var outputJpegStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "YUV_packed_line.jpg"), FileMode.OpenOrCreate))
            {
                image.Save(outputJpegStream);
            }
        }

        using (var image = new JpegImage(Media.Codecs.Image.ImageFormat.VUY(8), 696, 564))
        {
            for (int i = 0; i < image.Width; i += 16)
            {
                for (int j = 0; j < image.Height; j += 16)
                {
                    var data = image.GetVectorDataAt(i, j);

                    data = Vector<byte>.One * byte.MaxValue;

                    image.SetVectorDataAt(i, j, data);
                }
            }

            using (var outputJpegStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "VUY_packed_line.jpg"), FileMode.OpenOrCreate))
            {
                image.Save(outputJpegStream);
            }
        }

        using (var image = new JpegImage(Media.Codecs.Image.ImageFormat.CMYK(8), 696, 564))
        {
            for (int i = 0; i < image.Width; i += 16)
            {
                for (int j = 0; j < image.Height; j += 16)
                {
                    var data = image.GetPixelDataAt(x: i, y: j);
                    data.Fill(byte.MaxValue);
                }
            }

            using (var outputJpegStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "CMYK_planar_dots.jpg"), FileMode.OpenOrCreate))
            {
                image.Save(outputJpegStream);
            }
        }

        using (var image = new JpegImage(Media.Codecs.Image.ImageFormat.BGR(8), 696, 564))
        {
            for (int i = 0; i < image.Width; i += 16)
            {
                for (int j = 0; j < image.Height; j += 16)
                {
                    var data = image.GetVectorDataAt(x: i, y: j);
                    data = Vector<byte>.One * byte.MaxValue;
                    image.SetVectorDataAt(x: i, y: j, data);
                }
            }

            using (var outputJpegStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "bgr_packed_lines.jpg"), FileMode.OpenOrCreate))
            {
                image.Save(outputJpegStream);
            }
        }
    }

    private static void DumpMarker(Marker marker)
    {
        Console.Write("Function Code:");
        Console.WriteLine($"{marker.FunctionCode} ({marker.FunctionCode:X}) - {marker.ToString()}");
        if (marker.Length == 0) return;
        Console.Write("Length:");
        Console.WriteLine(marker.Length);
        Console.Write("Data:");
        if (marker.FunctionCode is Markers.TextComment)
        {
            using var textComment = new TextComment(marker);
            Console.Write(textComment.Comment);
        }
        else
        {
            using var slice = marker.Data;
            Console.Write(BitConverter.ToString(slice.Array, slice.Offset, Math.Min(16, slice.Count)));
        }
        if (marker.DataLength > 16)
            Console.WriteLine(" ...");
        else 
            Console.WriteLine();
    }

    private static void VerifyMarker(Marker marker)
    {
        using var memory = new MemorySegment(marker);
        using var data = marker.Data;
        var fromBytes = new Marker(marker.FunctionCode, marker.Length);
        data.CopyTo(fromBytes.Array, fromBytes.DataOffset);
        if(!memory.SequenceEqual(fromBytes))
            throw new InvalidDataException();
    }

    public static void TestLoad()
    {
        string currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;

        string jpegTestDir = Path.Combine(currentPath, "Media", "JpegTest");

        string outputDir = Path.Combine(currentPath, "Media", "JpegTest", "output");

        foreach (var filePath in Directory.GetFiles(jpegTestDir, "*.jpg"))
        {
            Console.WriteLine($"Processing file: {Path.GetFileName(filePath)}");

            var sourceMarkers = new List<Marker>();

            using var jpegStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Read);

            foreach (var marker in JpegCodec.ReadMarkers(jpegStream))
            {
                VerifyMarker(marker);
                DumpMarker(marker);
                sourceMarkers.Add(marker);
            }

            jpegStream.Seek(0, SeekOrigin.Begin);

            using var jpgImage = JpegImage.FromStream(jpegStream);

            var saveFileName = Path.Combine(outputDir, Path.GetFileName(filePath));

            using (var outputNew = new FileStream(saveFileName, FileMode.OpenOrCreate, FileAccess.Write))
            {
                jpgImage.Save(outputNew);
            }

            using (var inputNew = new FileStream(saveFileName, FileMode.OpenOrCreate, FileAccess.Read))
            {
                var destinationMarkers = new List<Marker>();

                foreach (var marker in JpegCodec.ReadMarkers(inputNew))
                {
                    VerifyMarker(marker);
                    DumpMarker(marker);
                }

                inputNew.Seek(0, SeekOrigin.Begin);

                using (var newJpgImage = JpegImage.FromStream(inputNew))
                {
                    if (newJpgImage.Width != jpgImage.Width ||
                        newJpgImage.Height != jpgImage.Height ||
                        newJpgImage.JpegState != jpgImage.JpegState ||
                        newJpgImage.ImageFormat.Components.Length != jpgImage.ImageFormat.Components.Length ||
                        newJpgImage.ImageFormat.Size != jpgImage.ImageFormat.Size)
                        throw new InvalidDataException();
                }
            }
        }
    }
}