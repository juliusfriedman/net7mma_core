using Media.Codec;
using Media.Codec.Jpeg;
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
        var format = ImageFormat.RGB(8);
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

        using (var image = new JpegImage(Media.Codecs.Image.ImageFormat.YUV(8), 696, 564))
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

    public static void TestLoad()
    {
        string currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;

        string jpegTestDir = Path.Combine(currentPath, "Media", "JpegTest");

        string outputDir = Path.Combine(currentPath, "Media", "JpegTest", "output");

        foreach (var filePath in Directory.GetFiles(jpegTestDir, "*.jpg"))
        {
            using var jpegStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Read);

            foreach (var marker in JpegCodec.ReadMarkers(jpegStream))
            {
                Console.Write("Function Code:");
                Console.WriteLine($"{marker.FunctionCode} ({marker.FunctionCode:X})");
                if (marker.Length == 0) continue;
                Console.Write("Data:");
                Console.WriteLine(BitConverter.ToString(marker.Data.Array, marker.Data.Offset, marker.Data.Count));
            }

            jpegStream.Seek(0, SeekOrigin.Begin);

            using var jpgImage = JpegImage.FromStream(jpegStream);

            var saveFileName = Path.Combine(outputDir, Path.GetFileName(filePath));

            using (var outputNew = new FileStream(saveFileName, FileMode.OpenOrCreate, FileAccess.Write))
            {
                jpgImage.Save(outputNew);
            }

            using var inputNew = new FileStream(saveFileName, FileMode.OpenOrCreate, FileAccess.Read);

            using var newJpgImage = JpegImage.FromStream(inputNew);

            if (newJpgImage.Width != jpgImage.Width ||
                newJpgImage.Height != jpgImage.Height ||
                newJpgImage.Progressive != jpgImage.Progressive ||
                newJpgImage.ImageFormat.Components.Length != jpgImage.ImageFormat.Components.Length)
                    throw new InvalidDataException();
        }
    }
}