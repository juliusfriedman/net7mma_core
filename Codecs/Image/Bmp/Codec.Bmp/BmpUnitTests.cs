using Codec.Bmp;
using Media.Codec;
using Media.Codecs.Image;
using Media.Common;
using System.Numerics;

namespace Media.UnitTests;

internal class BmpUnitTests
{
    public static void TestSaveBitmap()
    {
        var format = ImageFormat.RGB(8);
        var image = new BmpImage(format, 100, 100);
        using (var stream = new MemoryStream())
        {
            image.Save(stream);
            Console.WriteLine(stream.Length > 0 ? "Pass" : "Fail");
        }
    }

    public static void TestSave()
    {
        var currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        var outputDirectory = Directory.CreateDirectory(Path.Combine(currentPath!, "Media", "BmpTest", "output"));

        foreach (var dataLayout in Enum.GetValues<DataLayout>())
        {
            if (dataLayout == DataLayout.Unknown) continue;

            using (var image = new BmpImage(Media.Codecs.Image.ImageFormat.RGB(8, Binary.SystemByteOrder, dataLayout), 696, 564))
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

                using (var outputBmpStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"rgb_{dataLayout}.bmp"), FileMode.OpenOrCreate))
                {
                    image.Save(outputBmpStream);
                }

                using (var inputBmp = BmpImage.FromStream(new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"rgb_{dataLayout}.bmp"), FileMode.OpenOrCreate)))
                {
                    if (inputBmp.Width != 696) throw new Exception();

                    if (inputBmp.Height != 564) throw new Exception();
                }
            }
        }

        foreach (var dataLayout in Enum.GetValues<DataLayout>())
        {
            if (dataLayout == DataLayout.Unknown) continue;

            using (var image = new BmpImage(Media.Codecs.Image.ImageFormat.BGR(8, Binary.SystemByteOrder, dataLayout), 696, 564))
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

                using (var outputBmpStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"BGR_Vector_{dataLayout}.bmp"), FileMode.OpenOrCreate))
                {
                    image.Save(outputBmpStream);
                }
            }
        }

        using (var image = new BmpImage(ImageFormat.Binary(8), 696, 564))
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

            using (var outputBmpStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "Binary_packed_line2.bmp"), FileMode.OpenOrCreate))
            {
                image.Save(outputBmpStream);
            }
        }

        using (var image = new BmpImage(Media.Codecs.Image.ImageFormat.RGB(8), 696, 564))
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

            using (var outputBmpStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "rgb24_packed_line2.bmp"), FileMode.OpenOrCreate))
            {
                image.Save(outputBmpStream);
            }
        }

        using (var image = new BmpImage(Media.Codecs.Image.ImageFormat.RGB(8), 696, 564))
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

            using (var outputBmpStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "rgb24_packed_line3.bmp"), FileMode.OpenOrCreate))
            {
                image.Save(outputBmpStream);
            }
        }

        using (var image = new BmpImage(Media.Codecs.Image.ImageFormat.RGB(8), 696, 564))
        {
            for (int i = 0; i < image.Width; i += 16)
            {
                for (int j = 0; j < image.Height; j += 16)
                {
                    var data = image.GetPixelDataAt(x: i, y: j);
                    data.Fill(byte.MaxValue);
                }
            }

            using (var outputBmpStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "rgb24_packed_dots.bmp"), FileMode.OpenOrCreate))
            {
                image.Save(outputBmpStream);
            }
        }

        using (var image = new BmpImage(Media.Codecs.Image.ImageFormat.RGB(8), 696, 564))
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

            using (var outputBmpStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "rgb24_packed_lines.bmp"), FileMode.OpenOrCreate))
            {
                image.Save(outputBmpStream);
            }
        }
    }

    public static void TestLoad()
    {
        string currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;

        using var grayStream = new FileStream(Path.Combine(currentPath, "Media", "BmpTest", "lena_gray.bmp"), FileMode.Open, FileAccess.Read);
        using var grayImage = BmpImage.FromStream(grayStream);

        using var colorStream = new FileStream(Path.Combine(currentPath, "Media", "BmpTest", "lena_color.bmp"), FileMode.Open, FileAccess.Read);
        using var colorImage = BmpImage.FromStream(colorStream);

        if (colorImage.Width != grayImage.Width) throw new InvalidOperationException();
        if (colorImage.Height != grayImage.Height) throw new InvalidOperationException();

        var outputGray = new FileStream(Path.Combine(currentPath, "Media", "BmpTest", "output", "lena_gray_save.bmp"), FileMode.OpenOrCreate, FileAccess.Write);
        grayImage.Save(outputGray);

        var outputColor = new FileStream(Path.Combine(currentPath, "Media", "BmpTest", "output", "lena_color_save.bmp"), FileMode.OpenOrCreate, FileAccess.Write);
        colorImage.Save(outputColor);

        var newImage = new BmpImage(Codecs.Image.ImageFormat.RGB(8), grayImage.Width, grayImage.Height);
        grayImage.Data.CopyTo(newImage.Data);

        var outputNew = new FileStream(Path.Combine(currentPath, "Media", "BmpTest", "output", "lena_gray_new_save.bmp"), FileMode.OpenOrCreate, FileAccess.Write);
        newImage.Save(outputNew);
    }
}
