using Media.Common;
using System;
using System.Linq;
using System.Numerics;

namespace Media.Codecs.Image.Transformations;

public class YuvToRgbTransformation : ImageTransformation
{
    // Constructor for the YUV to RGB transformation
    public YuvToRgbTransformation(Image source, Image dest, Codec.TransformationQuality quality = Codec.TransformationQuality.None, bool shouldDispose = true)
        : base(source, dest, quality, shouldDispose)
    {
        // Check if the source and destination images have compatible formats
        if (!IsYuvImage(source.ImageFormat) || !IsRgbImage(dest.ImageFormat))
        {
            throw new ArgumentException("Invalid image formats. Source must be YUV and destination must be RGB.");
        }
    }

    // Check if the image format is a YUV format
    private static bool IsRgbImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.GetComponentById(ImageFormat.RedChannelId) != null &&
               format.GetComponentById(ImageFormat.GreenChannelId) != null &&
               format.GetComponentById(ImageFormat.BlueChannelId) != null;
    }

    // Check if the image format is a YUV format
    private static bool IsYuvImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.GetComponentById(ImageFormat.LumaChannelId) != null &&
               format.GetComponentById(ImageFormat.ChromaMajorChannelId) != null &&
               format.GetComponentById(ImageFormat.ChromaMinorChannelId) != null;
    }

    public override void Transform()
    {
        var rComponent = Destination.ImageFormat.GetComponentById(ImageFormat.RedChannelId);
        var rComponentIndex = Destination.GetComponentIndex(ImageFormat.RedChannelId);
        var rComponentData = new Common.MemorySegment(rComponent.Length);

        var gComponent = Destination.ImageFormat.GetComponentById(ImageFormat.GreenChannelId);
        var gComponentIndex = Destination.GetComponentIndex(ImageFormat.GreenChannelId);
        var gComponentData = new Common.MemorySegment(gComponent.Length);

        var bComponent = Destination.ImageFormat.GetComponentById(ImageFormat.BlueChannelId);
        var bComponentIndex = Destination.GetComponentIndex(ImageFormat.BlueChannelId);
        var bComponentData = new Common.MemorySegment(bComponent.Length);

        var yComponent = Source.ImageFormat.GetComponentById(ImageFormat.LumaChannelId);
        var uComponent = Source.ImageFormat.GetComponentById(ImageFormat.ChromaMajorChannelId);
        var vComponent = Source.ImageFormat.GetComponentById(ImageFormat.ChromaMinorChannelId);

        // Loop through each pixel in the source image
        for (int y = 0; y < Source.Height; y++)
        {
            for (int x = 0; x < Source.Width; x++)
            {
                // Read the YUV components for the current pixel
                var data = Source.GetComponentData(x, y, yComponent);
                var yValue = Binary.ReadBits(data.Array, data.Offset, yComponent.Size, false);

                data = Source.GetComponentData(x, y, uComponent);
                var uValue = Binary.ReadBits(data.Array, data.Offset, uComponent.Size, false);

                data = Source.GetComponentData(x, y, vComponent);
                var vValue = Binary.ReadBits(data.Array, data.Offset, vComponent.Size, false);

                // Convert YUV to RGB values
                double r = yValue + 1.13983 * (vValue - 128);
                double g = yValue - 0.39465 * (uValue - 128) - 0.58060 * (vValue - 128);
                double b = yValue + 2.03211 * (uValue - 128);

                // Clamp the RGB values to the range [0, 255]
                byte rByte = (byte)Math.Max(0, Math.Min(255, r));
                byte gByte = (byte)Math.Max(0, Math.Min(255, g));
                byte bByte = (byte)Math.Max(0, Math.Min(255, b));

                // Write the RGB components to the destination image
                Binary.WriteBits(rComponentData.Array, rComponentData.Offset, rComponent.Length, rByte, false);
                Destination.SetComponentData(x, y, rComponentIndex, rComponentData);

                Binary.WriteBits(gComponentData.Array, gComponentData.Offset, gComponent.Length, gByte, false);
                Destination.SetComponentData(x, y, gComponentIndex, gComponentData);

                Binary.WriteBits(bComponentData.Array, bComponentData.Offset, bComponent.Length, bByte, false);
                Destination.SetComponentData(x, y, bComponentIndex, bComponentData);
            }
        }
    }
}

public class VectorizedYuvToRgbTransformation : ImageTransformation
{
    // Constructor for the Vectorized YUV to RGB transformation
    public VectorizedYuvToRgbTransformation(Image source, Image dest, Codec.TransformationQuality quality = Codec.TransformationQuality.None, bool shouldDispose = true)
        : base(source, dest, quality, shouldDispose)
    {
        // Check if the source and destination images have compatible formats
        if (!IsYuvImage(source.ImageFormat) || !IsRgbImage(dest.ImageFormat))
        {
            throw new ArgumentException("Invalid image formats. Source must be YUV and destination must be RGB.");
        }
    }

    // Check if the image format is a YUV format
    private static bool IsRgbImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.GetComponentById(ImageFormat.RedChannelId) != null &&
               format.GetComponentById(ImageFormat.GreenChannelId) != null &&
               format.GetComponentById(ImageFormat.BlueChannelId) != null;
    }

    // Check if the image format is a YUV format
    private static bool IsYuvImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.GetComponentById(ImageFormat.LumaChannelId) != null &&
               format.GetComponentById(ImageFormat.ChromaMajorChannelId) != null &&
               format.GetComponentById(ImageFormat.ChromaMinorChannelId) != null;
    }

    public override void Transform()
    {
        int width = Source.Width;
        int height = Source.Height;

        // Prepare Vector<float> constants for conversion formulas
        Vector<float> vector1_13983 = new(1.13983f);
        Vector<float> vector0_39465 = new(0.39465f);
        Vector<float> vector0_58060 = new(0.58060f);
        Vector<float> vector2_03211 = new(2.03211f);
        Vector<float> vector128 = new(128f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x += Vector<float>.Count)
            {
                // Read the YUV components for the current block of pixels
                Vector<byte> yVector = Source.GetComponentVector(x + 0, y, ImageFormat.LumaChannelId);
                Vector<byte> uVector = Source.GetComponentVector(x + 1, y, ImageFormat.ChromaMajorChannelId);
                Vector<byte> vVector = Source.GetComponentVector(x + 2, y, ImageFormat.ChromaMinorChannelId);

                // Convert YUV to RGB using vectorized calculations
                Vector<float> yFloat = Vector.AsVectorSingle(yVector) - vector128;
                Vector<float> uFloat = Vector.AsVectorSingle(uVector) - vector128;
                Vector<float> vFloat = Vector.AsVectorSingle(vVector) - vector128;

                Vector<float> rValue = yFloat + vector1_13983 * vFloat;
                Vector<float> gValue = yFloat - vector0_39465 * uFloat - vector0_58060 * vFloat;
                Vector<float> bValue = yFloat + vector2_03211 * uFloat;

                // Clip values to [0, 255]
                rValue = Vector.Max(Vector.Min(rValue, vector128), Vector<float>.Zero);
                gValue = Vector.Max(Vector.Min(gValue, vector128), Vector<float>.Zero);
                bValue = Vector.Max(Vector.Min(bValue, vector128), Vector<float>.Zero);

                // Convert float vectors back to byte vectors
                Vector<byte> rByte = Vector.AsVectorByte(rValue);
                Vector<byte> gByte = Vector.AsVectorByte(gValue);
                Vector<byte> bByte = Vector.AsVectorByte(bValue);

                // Store RGB components in the destination image
                Destination.SetComponentVector(x + 0, y, ImageFormat.RedChannelId, rByte);
                Destination.SetComponentVector(x + 1, y, ImageFormat.GreenChannelId, gByte);
                Destination.SetComponentVector(x + 2, y, ImageFormat.BlueChannelId, bByte);
            }
        }
    }
}
