using Media.Common;
using System;
using System.Numerics;

namespace Media.Codecs.Image.Transformations;

//Todo Seperate into seperate assembly
public class RgbToLabTransformation : ImageTransformation
{
    // Constructor for the RGB to YUV transformation
    public RgbToLabTransformation(Image source, Image dest, Codec.TransformationQuality quality = Codec.TransformationQuality.None, bool shouldDispose = true)
        : base(source, dest, quality, shouldDispose)
    {
        // Check if the source and destination images have compatible formats
        if (!IsRgbImage(source.ImageFormat) || !IsLabImage(dest.ImageFormat))
        {
            throw new ArgumentException("Invalid image formats. Source must be RGB and destination must be YUV.");
        }
    }

    // Check if the image format is an RGB format
    private static bool IsRgbImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.GetComponentById(ImageFormat.RedChannelId) != null &&
               format.GetComponentById(ImageFormat.GreenChannelId) != null &&
               format.GetComponentById(ImageFormat.BlueChannelId) != null;
    }

    // Check if the image format is a YUV format
    private static bool IsLabImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.GetComponentById(ImageFormat.LChannelId) != null &&
               format.GetComponentById(ImageFormat.AChannelId) != null &&
               format.GetComponentById(ImageFormat.BChannelId) != null;
    }

    public override void Transform()
    {
        var lComponent = Destination.ImageFormat.GetComponentById(ImageFormat.LChannelId);
        var lComponentIndex = Destination.GetComponentIndex(ImageFormat.LChannelId);
        var lComponentData = new Common.MemorySegment(lComponent.Length);

        var aComponent = Destination.ImageFormat.GetComponentById(ImageFormat.AChannelId);
        var aComponentIndex = Destination.GetComponentIndex(ImageFormat.AChannelId);
        var aComponentData = new Common.MemorySegment(aComponent.Length);

        var bComponent = Destination.ImageFormat.GetComponentById(ImageFormat.BChannelId);
        var bComponentIndex = Destination.GetComponentIndex(ImageFormat.BChannelId);
        var bComponentData = new Common.MemorySegment(bComponent.Length);

        var redComponent = Source.ImageFormat.GetComponentById(ImageFormat.RedChannelId);
        var greenComponent = Source.ImageFormat.GetComponentById(ImageFormat.GreenChannelId);
        var blueComponent = Source.ImageFormat.GetComponentById(ImageFormat.BlueChannelId);

        for (int y = 0; y < Source.Height; y++)
        {
            for (int x = 0; x < Source.Width; x++)
            {
                var data = Source.GetComponentData(x, y, redComponent);
                var r = Binary.ReadBits(data.Array, data.Offset, redComponent.Size, false);

                data = Source.GetComponentData(x, y, greenComponent);
                var g = Binary.ReadBits(data.Array, data.Offset, greenComponent.Size, false);

                data = Source.GetComponentData(x, y, blueComponent);
                var b = Binary.ReadBits(data.Array, data.Offset, bComponent.Size, false);
                
                // Convert RGB to XYZ
                double X = r * 0.4124564 + g * 0.3575761 + b * 0.1804375;
                double Y = r * 0.2126729 + g * 0.7151522 + b * 0.0721750;
                double Z = r * 0.0193339 + g * 0.1191920 + b * 0.9503041;

                // Normalize for D65 white point
                X /= 0.95047;
                Y /= 1.00000;
                Z /= 1.08883;

                // Convert XYZ to Lab
                X = X > 0.008856 ? Math.Pow(X, 1.0 / 3.0) : (7.787 * X) + (16.0 / 116.0);
                Y = Y > 0.008856 ? Math.Pow(Y, 1.0 / 3.0) : (7.787 * Y) + (16.0 / 116.0);
                Z = Z > 0.008856 ? Math.Pow(Z, 1.0 / 3.0) : (7.787 * Z) + (16.0 / 116.0);

                double L = (116.0 * Y) - 16.0;
                double A = 500.0 * (x - Y);
                double B = 200.0 * (y - Z);

                // Store Lab components
                var lByte = (byte)(L * 2.55);
                var aByte = (byte)(A + 128);
                var bByte = (byte)(B + 128);

                Binary.WriteBits(lComponentData.Array, lComponentData.Offset, lComponent.Size, lByte, false);
                Destination.SetComponentData(x, y, lComponentIndex, lComponentData);

                Binary.WriteBits(aComponentData.Array, aComponentData.Offset, aComponent.Size, aByte, false);
                Destination.SetComponentData(x, y, aComponentIndex, aComponentData);

                Binary.WriteBits(bComponentData.Array, bComponentData.Offset, bComponent.Size, bByte, false);
                Destination.SetComponentData(x, y, bComponentIndex, bComponentData);
            }
        }
    }
}

/// <summary>
/// Not working.
/// </summary>
public class VectorizedRgbToLabTransformation : ImageTransformation
{
    // Constructor for the RGB to YUV transformation
    public VectorizedRgbToLabTransformation(Image source, Image dest, Codec.TransformationQuality quality = Codec.TransformationQuality.None, bool shouldDispose = true)
        : base(source, dest, quality, shouldDispose)
    {
        // Check if the source and destination images have compatible formats
        if (!IsRgbImage(source.ImageFormat) || !IsLabImage(dest.ImageFormat))
        {
            throw new ArgumentException("Invalid image formats. Source must be RGB and destination must be YUV.");
        }
    }

    // Check if the image format is an RGB format
    private static bool IsRgbImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.GetComponentById(ImageFormat.RedChannelId) != null &&
               format.GetComponentById(ImageFormat.GreenChannelId) != null &&
               format.GetComponentById(ImageFormat.BlueChannelId) != null;
    }

    // Check if the image format is a YUV format
    private static bool IsLabImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.GetComponentById(ImageFormat.LChannelId) != null &&
               format.GetComponentById(ImageFormat.AChannelId) != null &&
               format.GetComponentById(ImageFormat.BChannelId) != null;
    }

    //Could chain conversion using RbgToXyz transformation...

    public override void Transform()
    {
        int width = Source.Width;
        int height = Source.Height;

        // Prepare Vector<float> constants for conversion formulas
        Vector<float> vector0_4124564 = new(0.4124564f);
        Vector<float> vector0_3575761 = new(0.3575761f);
        Vector<float> vector0_1804375 = new(0.1804375f);
        Vector<float> vector0_2126729 = new(0.2126729f);
        Vector<float> vector0_7151522 = new(0.7151522f);
        Vector<float> vector0_0721750 = new(0.0721750f);
        Vector<float> vector0_0193339 = new(0.0193339f);
        Vector<float> vector0_1191920 = new(0.1191920f);
        Vector<float> vector0_9503041 = new(0.9503041f);
        Vector<float> vector0_95047 = new(0.95047f);
        Vector<float> vector1_00000 = new(1.00000f);
        Vector<float> vector1_08883 = new(1.08883f);
        Vector<float> vector7_787 = new(7.787f);
        Vector<float> vector16_116 = new(16.0f / 116.0f);
        Vector<float> vector116 = new(116.0f);
        Vector<float> vector500 = new(500.0f);
        Vector<float> vector200 = new(200.0f);
        Vector<float> vector2_55 = new(2.55f);
        Vector<float> vector128 = new(128f);
        Vector<float> vector0_008856 = new(0.008856f);
        Vector<float> vector1_0_3 = new(1.0f / 3.0f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x += Vector<float>.Count)
            {
                // Read RGB components
                var r = Vector.AsVectorSingle(Source.GetComponentVector(x, y, ImageFormat.RedChannelId));
                var g = Vector.AsVectorSingle(Source.GetComponentVector(x, y, ImageFormat.GreenChannelId));
                var b = Vector.AsVectorSingle(Source.GetComponentVector(x, y, ImageFormat.BlueChannelId));

                // Convert RGB to XYZ
                var X = r * vector0_4124564 + g * vector0_3575761 + b * vector0_1804375;
                var Y = r * vector0_2126729 + g * vector0_7151522 + b * vector0_0721750;
                var Z = r * vector0_0193339 + g * vector0_1191920 + b * vector0_9503041;

                // Normalize for D65 white point
                X /= vector0_95047;
                Y /= vector1_00000;
                Z /= vector1_08883;

                // Convert XYZ to Lab
                X = Vector.ConditionalSelect(Vector.GreaterThan(X, vector0_008856), Vector.SquareRoot(X), vector7_787 * X + vector16_116);
                Y = Vector.ConditionalSelect(Vector.GreaterThan(Y, vector0_008856), Vector.SquareRoot(Y), vector7_787 * Y + vector16_116);
                Z = Vector.ConditionalSelect(Vector.GreaterThan(Z, vector0_008856), Vector.SquareRoot(Z), vector7_787 * Z + vector16_116);

                var L = vector116 * Y - new Vector<float>(16.0f);
                var A = vector500 * (X - Y);
                var B = vector200 * (Y - Z);

                // Store Lab components
                var lByte = Vector.ConvertToInt32(L * vector2_55);
                var aByte = Vector.ConvertToInt32(A + vector128);
                var bByte = Vector.ConvertToInt32(B + vector128);

                Destination.SetComponentVector(x, y, ImageFormat.LChannelId, Vector.AsVectorByte(lByte));
                Destination.SetComponentVector(x, y, ImageFormat.AChannelId, Vector.AsVectorByte(aByte));
                Destination.SetComponentVector(x, y, ImageFormat.BChannelId, Vector.AsVectorByte(bByte));
            }
        }
    }
}
