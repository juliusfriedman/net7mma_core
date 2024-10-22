using Media.Common;
using System;
using System.Numerics;

namespace Media.Codecs.Image.Transformations;

public class RgbToXyzTransformation : ImageTransformation
{
    // Constructor for the RGB to XYZ transformation
    public RgbToXyzTransformation(Image source, Image dest, Codec.TransformationQuality quality = Codec.TransformationQuality.None, bool shouldDispose = true)
        : base(source, dest, quality, shouldDispose)
    {
        // Check if the source and destination images have compatible formats
        if (!IsRgbImage(source.ImageFormat) || !IsXyzImage(dest.ImageFormat))
        {
            throw new ArgumentException("Invalid image formats. Source must be RGB and destination must be XYZ.");
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

    // Check if the image format is an XYZ format
    private static bool IsXyzImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.GetComponentById(ImageFormat.XChannelId) != null &&
               format.GetComponentById(ImageFormat.YChannelId) != null &&
               format.GetComponentById(ImageFormat.ZChannelId) != null;
    }

    public override void Transform()
    {
        var xComponent = Destination.ImageFormat.GetComponentById(ImageFormat.XChannelId);
        var xComponentIndex = Destination.GetComponentIndex(ImageFormat.XChannelId);
        var xComponentData = new Common.MemorySegment(xComponent.Length);

        var yComponent = Destination.ImageFormat.GetComponentById(ImageFormat.YChannelId);
        var yComponentIndex = Destination.GetComponentIndex(ImageFormat.YChannelId);
        var yComponentData = new Common.MemorySegment(yComponent.Length);

        var zComponent = Destination.ImageFormat.GetComponentById(ImageFormat.ZChannelId);
        var zComponentIndex = Destination.GetComponentIndex(ImageFormat.ZChannelId);
        var zComponentData = new Common.MemorySegment(zComponent.Length);

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
                var b = Binary.ReadBits(data.Array, data.Offset, blueComponent.Size, false);

                // Convert RGB to XYZ
                double X = r * 0.4124564 + g * 0.3575761 + b * 0.1804375;
                double Y = r * 0.2126729 + g * 0.7151522 + b * 0.0721750;
                double Z = r * 0.0193339 + g * 0.1191920 + b * 0.9503041;

                // Store XYZ components
                var xByte = (byte)(X * 255.0 / 100.0);
                var yByte = (byte)(Y * 255.0 / 100.0);
                var zByte = (byte)(Z * 255.0 / 100.0);

                Binary.WriteBits(xComponentData.Array, xComponentData.Offset, xComponent.Size, xByte, false);
                Destination.SetComponentData(x, y, xComponentIndex, xComponentData);

                Binary.WriteBits(yComponentData.Array, yComponentData.Offset, yComponent.Size, yByte, false);
                Destination.SetComponentData(x, y, yComponentIndex, yComponentData);

                Binary.WriteBits(zComponentData.Array, zComponentData.Offset, zComponent.Size, zByte, false);
                Destination.SetComponentData(x, y, zComponentIndex, zComponentData);
            }
        }
    }
}

public class VectorizedRgbToXyzTransformation : ImageTransformation
{
    // Constructor for the RGB to YUV transformation
    public VectorizedRgbToXyzTransformation(Image source, Image dest, Codec.TransformationQuality quality = Codec.TransformationQuality.None, bool shouldDispose = true)
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

                // Convert to byte and store XYZ components
                var xByte = Vector.ConvertToInt32(X);
                var yByte = Vector.ConvertToInt32(Y);
                var zByte = Vector.ConvertToInt32(Z);

                Destination.SetComponentVector(x, y, ImageFormat.LChannelId, Vector.AsVectorByte(xByte));
                Destination.SetComponentVector(x, y, ImageFormat.AChannelId, Vector.AsVectorByte(yByte));
                Destination.SetComponentVector(x, y, ImageFormat.BChannelId, Vector.AsVectorByte(zByte));
            }
        }
    }
}
