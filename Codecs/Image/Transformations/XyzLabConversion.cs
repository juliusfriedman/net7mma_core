using System;
using System.Numerics;

namespace Media.Codecs.Image.Transformations;

public class XyzLabConversion
    : ImageTransformation
{
    public XyzLabConversion(Image source, Image dest, Codec.TransformationQuality quality = Codec.TransformationQuality.None, bool shouldDispose = true)
        : base(source, dest, quality, shouldDispose)
    {
        if (!IsXyzImage(source.ImageFormat) || !IsLabImage(dest.ImageFormat))
        {
            throw new ArgumentException("Invalid image formats. Source must be XYZ and destination must be Lab.");
        }
    }

    private static bool IsXyzImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.GetComponentById(ImageFormat.XChannelId) != null &&
               format.GetComponentById(ImageFormat.YChannelId) != null &&
               format.GetComponentById(ImageFormat.ZChannelId) != null;
    }

    private static bool IsLabImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.GetComponentById(ImageFormat.LChannelId) != null &&
               format.GetComponentById(ImageFormat.AChannelId) != null &&
               format.GetComponentById(ImageFormat.BChannelId) != null;
    }

    /// <summary>
    /// From Xyz to Lab
    /// </summary>
    public override void Transform()
    {
        int width = Source.Width;
        int height = Source.Height;

        // Prepare Vector<float> constants for conversion formulas
        Vector<float> vector0_008856 = new(0.008856f);
        Vector<float> vector7_787 = new(7.787f);
        Vector<float> vector16_116 = new(16.0f / 116.0f);
        Vector<float> vector116 = new(116.0f);
        Vector<float> vector500 = new(500.0f);
        Vector<float> vector200 = new(200.0f);
        Vector<float> vector128 = new(128f);
        Vector<float> vector2_55 = new(2.55f);
        Vector<float> vector0_95047 = new(0.95047f);
        Vector<float> vector1_00000 = new(1.00000f);
        Vector<float> vector1_08883 = new(1.08883f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x += Vector<float>.Count)
            {
                // Read XYZ components
                var X = Vector.AsVectorSingle(Source.GetComponentVector(x, y, ImageFormat.XChannelId));
                var Y = Vector.AsVectorSingle(Source.GetComponentVector(x, y, ImageFormat.YChannelId));
                var Z = Vector.AsVectorSingle(Source.GetComponentVector(x, y, ImageFormat.ZChannelId));

                // Normalize for D65 white point
                X /= vector0_95047;
                Y /= vector1_00000;
                Z /= vector1_08883;

                // Convert XYZ to Lab
                X = Vector.ConditionalSelect(Vector.GreaterThan(X, vector0_008856), Vector.SquareRoot(Vector.SquareRoot(X * X * X)), vector7_787 * X + vector16_116);
                Y = Vector.ConditionalSelect(Vector.GreaterThan(Y, vector0_008856), Vector.SquareRoot(Vector.SquareRoot(Y * Y * Y)), vector7_787 * Y + vector16_116);
                Z = Vector.ConditionalSelect(Vector.GreaterThan(Z, vector0_008856), Vector.SquareRoot(Vector.SquareRoot(Z * Z * Z)), vector7_787 * Z + vector16_116);

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

    /// <summary>
    /// Todo add to interface...
    /// From Lab to Xyz
    /// </summary>
    public void ReverseTransform()
    {
        int width = Destination.Width;
        int height = Destination.Height;

        // Prepare Vector<float> constants for reverse conversion formulas
        Vector<float> vector0_008856 = new(0.008856f);
        Vector<float> vector7_787 = new(7.787f);
        Vector<float> vector16_116 = new(16.0f / 116.0f);
        Vector<float> vector116 = new(116.0f);
        Vector<float> vector500 = new(500.0f);
        Vector<float> vector200 = new(200.0f);
        Vector<float> vector128 = new(128f);
        Vector<float> vector2_55 = new(2.55f);
        Vector<float> vector0_95047 = new(0.95047f);
        Vector<float> vector1_00000 = new(1.00000f);
        Vector<float> vector1_08883 = new(1.08883f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x += Vector<float>.Count)
            {
                // Read Lab components
                var L = Vector.AsVectorSingle(Destination.GetComponentVector(x, y, ImageFormat.LChannelId)) / vector2_55;
                var A = Vector.AsVectorSingle(Destination.GetComponentVector(x, y, ImageFormat.AChannelId)) - vector128;
                var B = Vector.AsVectorSingle(Destination.GetComponentVector(x, y, ImageFormat.BChannelId)) - vector128;

                // Convert Lab to XYZ
                var Y = (L + new Vector<float>(16.0f)) / vector116;
                var X = A / vector500 + Y;
                var Z = Y - B / vector200;

                X = Vector.ConditionalSelect(Vector.GreaterThan(X, vector16_116), X * X * X, (X - vector16_116) / vector7_787);
                Y = Vector.ConditionalSelect(Vector.GreaterThan(Y, vector16_116), Y * Y * Y, (Y - vector16_116) / vector7_787);
                Z = Vector.ConditionalSelect(Vector.GreaterThan(Z, vector16_116), Z * Z * Z, (Z - vector16_116) / vector7_787);

                // Denormalize for D65 white point
                X *= vector0_95047;
                Y *= vector1_00000;
                Z *= vector1_08883;

                // Store XYZ components
                Destination.SetComponentVector(x, y, ImageFormat.XChannelId, Vector.AsVectorByte(Vector.ConvertToInt32(X)));
                Destination.SetComponentVector(x, y, ImageFormat.YChannelId, Vector.AsVectorByte(Vector.ConvertToInt32(Y)));
                Destination.SetComponentVector(x, y, ImageFormat.ZChannelId, Vector.AsVectorByte(Vector.ConvertToInt32(Z)));
            }
        }
    }
}