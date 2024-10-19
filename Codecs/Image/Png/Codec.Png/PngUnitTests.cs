using Codec.Png;
using Media.Codec;
using Media.Codecs.Image;
using Media.Common;
using System.Numerics;

namespace Media.UnitTests;

internal class PngUnitTests
{
    public static void TestSaveBitmap()
    {
        var format = ImageFormat.RGBA(8);
        var image = new PngImage(format, 100, 100);
        using (var stream = new MemoryStream())
        {
            image.Save(stream);
            Console.WriteLine(stream.Length > 0 ? "Pass" : "Fail");
        }
    }

    public static void TestSave()
    {
        var currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        var outputDirectory = Directory.CreateDirectory(Path.Combine(currentPath!, "Media", "PngTest", "output"));

        foreach (var dataLayout in Enum.GetValues<DataLayout>())
        {
            if (dataLayout == DataLayout.Unknown) continue;

            using (var image = new PngImage(Media.Codecs.Image.ImageFormat.RGBA(8, Binary.SystemByteOrder, dataLayout), 696, 564))
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

                using (var outputPngStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"rgba_{dataLayout}.png"), FileMode.OpenOrCreate))
                {
                    image.Save(outputPngStream);
                }

                using (var inputPng = PngImage.FromStream(new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"rgba_{dataLayout}.png"), FileMode.OpenOrCreate)))
                {
                    if (inputPng.Width != 696) throw new Exception();

                    if (inputPng.Height != 564) throw new Exception();
                }
            }
        }

        foreach (var dataLayout in Enum.GetValues<DataLayout>())
        {
            if (dataLayout == DataLayout.Unknown) continue;

            using (var image = new PngImage(Media.Codecs.Image.ImageFormat.RGB(8, Binary.SystemByteOrder, dataLayout), 696, 564))
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

                using (var outputPngStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"RGB_Vector_{dataLayout}.png"), FileMode.OpenOrCreate))
                {
                    image.Save(outputPngStream);
                }
            }
        }

        using (var image = new PngImage(ImageFormat.Binary(8), 696, 564))
        {
            for (int i = 0; i < image.Width; i += 4)
            {
                for (int j = 0; j < image.Height; j += 4)
                {
                    for (int c = 0; c < image.ImageFormat.Components.Length; ++c)
                    {
                        var data = image.GetComponentVector(i, j, c);

                        data = Vector<byte>.One * byte.MaxValue;

                        image.SetComponentVector(i, j, c, data);
                    }
                }
            }

            using (var outputPngStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "Binary_packed_line2.png"), FileMode.OpenOrCreate))
            {
                image.Save(outputPngStream);
            }
        }

        using (var image = new PngImage(Media.Codecs.Image.ImageFormat.RGBA(8), 696, 564))
        {
            for (int i = 0; i < image.Width; i += 4)
            {
                for (int j = 0; j < image.Height; j += 4)
                {
                    for (int c = 0; c < image.ImageFormat.Components.Length; ++c)
                    {
                        var data = image.GetComponentVector(i, j, c);

                        data = Vector<byte>.One * byte.MaxValue;

                        image.SetComponentVector(i, j, c, data);
                    }
                }
            }

            using (var outputPngStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "rgba_packed_line2.png"), FileMode.OpenOrCreate))
            {
                image.Save(outputPngStream);
            }
        }

        using (var image = new PngImage(Media.Codecs.Image.ImageFormat.RGBA(8), 696, 564))
        {
            for (int i = 0; i < image.Width; i += 16)
            {
                for (int j = 0; j < image.Height; j += 16)
                {
                    for (int c = 0; c < image.ImageFormat.Length; ++c)
                    {
                        var data = image.GetComponentVector(i, j, c);

                        data = Vector<byte>.One * byte.MaxValue;

                        image.SetComponentVector(i, j, c, data);
                    }
                }
            }

            using (var outputPngStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "rgba_packed_line3.png"), FileMode.OpenOrCreate))
            {
                image.Save(outputPngStream);
            }
        }

        using (var image = new PngImage(Media.Codecs.Image.ImageFormat.RGBA(8), 696, 564))
        {
            for (int i = 0; i < image.Width; i += 16)
            {
                for (int j = 0; j < image.Height; j += 16)
                {
                    var data = image.GetPixelDataAt(x: i, y: j);
                    data.Fill(byte.MaxValue);
                }
            }

            using (var outputPngStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "rgba_packed_dots.png"), FileMode.OpenOrCreate))
            {
                image.Save(outputPngStream);
            }
        }

        using (var image = new PngImage(Media.Codecs.Image.ImageFormat.RGBA(8), 696, 564))
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

            using (var outputPngStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "rgba_packed_lines.png"), FileMode.OpenOrCreate))
            {
                image.Save(outputPngStream);
            }
        }
    }

    public static void TestLoad()
    {
        string currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;

        using var grayStream = new FileStream(Path.Combine(currentPath, "Media", "PngTest", "lena_gray.png"), FileMode.Open, FileAccess.Read);
        using var grayImage = PngImage.FromStream(grayStream);

        using var colorStream = new FileStream(Path.Combine(currentPath, "Media", "PngTest", "lena_color.png"), FileMode.Open, FileAccess.Read);
        using var colorImage = PngImage.FromStream(colorStream);

        if (colorImage.Width != grayImage.Width) throw new InvalidOperationException();
        if (colorImage.Height != grayImage.Height) throw new InvalidOperationException();

        var outputGray = new FileStream(Path.Combine(currentPath, "Media", "PngTest", "output", "lena_gray_save.png"), FileMode.OpenOrCreate, FileAccess.Write);
        grayImage.Save(outputGray);

        var outputColor = new FileStream(Path.Combine(currentPath, "Media", "PngTest", "output", "lena_color_save.png"), FileMode.OpenOrCreate, FileAccess.Write);
        colorImage.Save(outputColor);

        var newImage = new PngImage(Codecs.Image.ImageFormat.RGBA(8), grayImage.Width, grayImage.Height);
        grayImage.Data.CopyTo(newImage.Data);

        var outputNew = new FileStream(Path.Combine(currentPath, "Media", "PngTest", "output", "lena_gray_new_save.png"), FileMode.OpenOrCreate, FileAccess.Write);
        newImage.Save(outputNew);
    }
}