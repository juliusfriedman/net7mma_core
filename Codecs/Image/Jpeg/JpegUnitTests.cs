using Codec.Jpeg;
using Media.Codec;
using Media.Codecs.Image;
using Media.Common;
using System;
using System.IO;
using System.Numerics;

namespace Media.UnitTests;

internal class JpegUnitTests
{
    public static void TestSave()
    {
        var format = ImageFormat.RGBA(8);
        using (var image = new JpegImage(format, 100, 100))
        {
            using (var stream = new MemoryStream())
            {
                image.Save(stream);
                Console.WriteLine(stream.Length > 0 ? "Pass" : "Fail");
            }
        }

        var currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        var outputDirectory = Directory.CreateDirectory(Path.Combine(currentPath!, "Media", "JpegTest", "output"));

        foreach (var dataLayout in Enum.GetValues<DataLayout>())
        {
            if (dataLayout == DataLayout.Unknown) continue;

            using (var image = new JpegImage(Media.Codecs.Image.ImageFormat.RGBA(8, Binary.SystemByteOrder, dataLayout), 696, 564))
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

                using (var outputPngStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"rgba_{dataLayout}.jpg"), FileMode.OpenOrCreate))
                {
                    image.Save(outputPngStream);
                }

                using (var inputPng = JpegImage.FromStream(new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"rgba_{dataLayout}.jpg"), FileMode.OpenOrCreate)))
                {
                    if (inputPng.Width != 696) throw new Exception();

                    if (inputPng.Height != 564) throw new Exception();
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

                using (var outputPngStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"RGB_Vector_{dataLayout}.jpg"), FileMode.OpenOrCreate))
                {
                    image.Save(outputPngStream);
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

            using (var outputPngStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "Binary_packed_line2.jpg"), FileMode.OpenOrCreate))
            {
                image.Save(outputPngStream);
            }
        }

        using (var image = new JpegImage(Media.Codecs.Image.ImageFormat.RGBA(8), 696, 564))
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

            using (var outputPngStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "rgba_packed_line2.jpg"), FileMode.OpenOrCreate))
            {
                image.Save(outputPngStream);
            }
        }

        using (var image = new JpegImage(Media.Codecs.Image.ImageFormat.RGBA(8), 696, 564))
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

            using (var outputPngStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "rgba_packed_line3.jpg"), FileMode.OpenOrCreate))
            {
                image.Save(outputPngStream);
            }
        }

        using (var image = new JpegImage(Media.Codecs.Image.ImageFormat.RGBA(8), 696, 564))
        {
            for (int i = 0; i < image.Width; i += 16)
            {
                for (int j = 0; j < image.Height; j += 16)
                {
                    var data = image.GetPixelDataAt(x: i, y: j);
                    data.Fill(byte.MaxValue);
                }
            }

            using (var outputPngStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "rgba_packed_dots.jpg"), FileMode.OpenOrCreate))
            {
                image.Save(outputPngStream);
            }
        }

        using (var image = new JpegImage(Media.Codecs.Image.ImageFormat.RGBA(8), 696, 564))
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

            using (var outputPngStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "rgba_packed_lines.jpg"), FileMode.OpenOrCreate))
            {
                image.Save(outputPngStream);
            }
        }
    }

    public static void TestLoad()
    {
        string currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;

        string pngTestDir = Path.Combine(currentPath, "Media", "JpegTest");

        string outputDir = Path.Combine(currentPath, "Media", "JpegTest", "output");

        foreach (var filePath in Directory.GetFiles(pngTestDir, "*.jpg"))
        {
            using var jpegStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Read);

            foreach (var marker in JpegCodec.ReadMarkers(jpegStream))
            {
                Console.Write("PrefixLength:");
                Console.WriteLine(marker.PrefixLength);
                Console.Write("Function Code:");
                Console.WriteLine(marker.Code);
                Console.Write("Data:");
                Console.WriteLine(BitConverter.ToString(marker.Data));
            }

            jpegStream.Seek(0, SeekOrigin.Begin);

            using var pngImage = JpegImage.FromStream(jpegStream);

            var saveFileName = Path.Combine(outputDir, Path.GetFileName(filePath));

            using var outputNew = new FileStream(saveFileName, FileMode.OpenOrCreate, FileAccess.Write);
            pngImage.Save(outputNew);
        }
    }
}