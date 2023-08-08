using Media.Common;
using System;
using System.Linq;
using System.Numerics;

namespace Media.Codecs.Image.Transformations;

//Todo Seperate into seperate assembly
public class RgbToYuvImageTransformation : ImageTransformation
{
    // Constructor for the RGB to YUV transformation
    public RgbToYuvImageTransformation(Image source, Image dest, Codec.TransformationQuality quality = Codec.TransformationQuality.None, bool shouldDispose = true)
        : base(source, dest, quality, shouldDispose)
    {
        // Check if the source and destination images have compatible formats
        if (!IsRgbImage(source.ImageFormat) || !IsYuvImage(dest.ImageFormat))
        {
            throw new ArgumentException("Invalid image formats. Source must be RGB and destination must be YUV.");
        }
    }

    // Check if the image format is an RGB format
    private static bool IsRgbImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.Components.Any(c => c.Id == ImageFormat.RedChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.GreenChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.BlueChannelId);
    }

    // Check if the image format is a YUV format
    private static bool IsYuvImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.Components.Any(c => c.Id == ImageFormat.LumaChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.ChromaMajorChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.ChromaMinorChannelId);
    }

    public override void Transform()
    {
        var yComponent = Destination.ImageFormat.GetComponentById(ImageFormat.LumaChannelId);
        var yComponentIndex = Destination.GetComponentIndex(ImageFormat.LumaChannelId);
        var yComponentData = new Common.MemorySegment(yComponent.Length);

        var uComponent = Destination.ImageFormat.GetComponentById(ImageFormat.ChromaMajorChannelId);
        var uComponentIndex = Destination.GetComponentIndex(ImageFormat.ChromaMajorChannelId);
        var uComponentData = new Common.MemorySegment(uComponent.Length);

        var vComponent = Destination.ImageFormat.GetComponentById(ImageFormat.ChromaMinorChannelId);
        var vComponentIndex = Destination.GetComponentIndex(ImageFormat.ChromaMinorChannelId);
        var vComponentData = new Common.MemorySegment(vComponent.Length);

        var rComponent = Source.ImageFormat.GetComponentById(ImageFormat.RedChannelId);
        var gComponent = Source.ImageFormat.GetComponentById(ImageFormat.GreenChannelId);
        var bComponent = Source.ImageFormat.GetComponentById(ImageFormat.BlueChannelId);

        for (int y = 0; y < Source.Height; y++)
        {
            for (int x = 0; x < Source.Width; x++)
            {
                var data = Source.GetComponentData(x, y, rComponent);
                var r = Binary.ReadBits(data.Array, data.Offset, rComponent.Size, false);

                data = Source.GetComponentData(x, y, gComponent);
                var g = Binary.ReadBits(data.Array, data.Offset, gComponent.Size, false);

                data = Source.GetComponentData(x, y, bComponent);
                var b = Binary.ReadBits(data.Array, data.Offset, bComponent.Size, false);

                double yValue = 0.299 * r + 0.587 * g + 0.114 * b;
                double uValue = -0.14713 * r - 0.28886 * g + 0.436 * b;
                double vValue = 0.615 * r - 0.51498 * g - 0.10001 * b;

                byte yByte = (byte)Math.Max(0, Math.Min(255, yValue));
                byte uByte = (byte)Math.Max(0, Math.Min(255, uValue + 128));
                byte vByte = (byte)Math.Max(0, Math.Min(255, vValue + 128));

                Binary.WriteBits(yComponentData.Array, yComponentData.Offset, yComponent.Size, yByte, false);
                Destination.SetComponentData(x, y, yComponentIndex, yComponentData);

                Binary.WriteBits(uComponentData.Array, uComponentData.Offset, uComponent.Size, uByte, false);
                Destination.SetComponentData(x, y, uComponentIndex, uComponentData);

                Binary.WriteBits(vComponentData.Array, vComponentData.Offset, vComponent.Size, vByte, false);
                Destination.SetComponentData(x, y, vComponentIndex, vComponentData);
            }
        }
    }
}

/// <summary>
/// Not working.
/// </summary>
public class VectorizedRgbToYuvImageTransformation : ImageTransformation
{
    // Constructor for the RGB to YUV transformation
    public VectorizedRgbToYuvImageTransformation(Image source, Image dest, Codec.TransformationQuality quality = Codec.TransformationQuality.None, bool shouldDispose = true)
        : base(source, dest, quality, shouldDispose)
    {
        // Check if the source and destination images have compatible formats
        if (!IsRgbImage(source.ImageFormat) || !IsYuvImage(dest.ImageFormat))
        {
            throw new ArgumentException("Invalid image formats. Source must be RGB and destination must be YUV.");
        }
    }

    // Check if the image format is an RGB format
    private static bool IsRgbImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.Components.Any(c => c.Id == ImageFormat.RedChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.GreenChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.BlueChannelId);
    }

    // Check if the image format is a YUV format
    private static bool IsYuvImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.Components.Any(c => c.Id == ImageFormat.LumaChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.ChromaMajorChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.ChromaMinorChannelId);
    }

    public override void Transform()
    {
        int width = Source.Width;
        int height = Source.Height;

        // Prepare Vector<float> constants for conversion formulas
        Vector<float> vector0_299 = new Vector<float>(0.299f);
        Vector<float> vector0_587 = new Vector<float>(0.587f);
        Vector<float> vector0_114 = new Vector<float>(0.114f);
        Vector<float> vector128 = new Vector<float>(128f);
        Vector<float> vector255 = new Vector<float>(255f);

        // Process the image in 4-pixel blocks (assuming RGBA format)
        int blockSize = 4;
        int blockWidth = width / blockSize * blockSize;

        var yComponentIndex = Destination.GetComponentIndex(ImageFormat.LumaChannelId);
        var yComponent = Destination.ImageFormat.GetComponentById(ImageFormat.LumaChannelId);
        var yComponentData = new Common.MemorySegment(yComponent.Length);

        var uComponentIndex = Destination.GetComponentIndex(ImageFormat.ChromaMajorChannelId);
        var uComponent = Destination.ImageFormat.GetComponentById(ImageFormat.ChromaMajorChannelId);
        var uComponentData = new Common.MemorySegment(uComponent.Length);

        var vComponentIndex = Destination.GetComponentIndex(ImageFormat.ChromaMinorChannelId);
        var vComponent = Destination.ImageFormat.GetComponentById(ImageFormat.ChromaMinorChannelId);
        var vComponentData = new Common.MemorySegment(vComponent.Length);

        var rComponent = Source.ImageFormat.GetComponentById(ImageFormat.RedChannelId);
        var rComponentIndex = Source.GetComponentIndex(ImageFormat.RedChannelId);
        var gComponent = Source.ImageFormat.GetComponentById(ImageFormat.GreenChannelId);
        var gComponentIndex = Source.GetComponentIndex(ImageFormat.GreenChannelId);
        var bComponent = Source.ImageFormat.GetComponentById(ImageFormat.BlueChannelId);
        var bComponentIndex = Source.GetComponentIndex(ImageFormat.BlueChannelId);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < blockWidth; x += blockSize)
            {
                // Load the pixel data into Vector<float> arrays
                Vector<byte> r = Source.GetComponentVector(x + 0, y, rComponentIndex);
                Vector<byte> g = Source.GetComponentVector(x + 1, y, gComponentIndex);
                Vector<byte> b = Source.GetComponentVector(x + 2, y, bComponentIndex);

                // Convert RGB to YUV using vectorized calculations
                Vector<float> rFloat = (Vector<float>)r;
                Vector<float> gFloat = (Vector<float>)g;
                Vector<float> bFloat = (Vector<float>)b;

                Vector<float> yValue = vector0_299 * rFloat + vector0_587 * gFloat + vector0_114 * bFloat;
                Vector<float> uValue = -0.14713f * rFloat - 0.28886f * gFloat + 0.436f * bFloat;
                Vector<float> vValue = 0.615f * rFloat - 0.51498f * gFloat - 0.10001f * bFloat;

                // Clip values to [0, 255]
                yValue = Vector.Max(Vector.Min(yValue, vector255), Vector<float>.Zero);
                uValue = Vector.Max(Vector.Min(uValue + vector128, vector255), Vector<float>.Zero);
                vValue = Vector.Max(Vector.Min(vValue + vector128, vector255), Vector<float>.Zero);

                // Convert float vectors back to byte vectors
                Vector<byte> yByte = Vector.AsVectorByte(yValue);
                Vector<byte> uByte = Vector.AsVectorByte(uValue);
                Vector<byte> vByte = Vector.AsVectorByte(vValue);

                // Store YUV components in the destination image
                Destination.SetComponentData(x + 0, y, yComponentIndex, yByte);
                Destination.SetComponentData(x + 1, y, uComponentIndex, uByte);
                Destination.SetComponentData(x + 2, y, vComponentIndex, vByte);
            }

            // Process any remaining pixels (not in 4-pixel blocks)
            for (int x = blockWidth; x < width; x++)
            {
                var data = Source.GetComponentData(x, y, rComponent);
                var r = Binary.ReadBits(data.Array, data.Offset, rComponent.Size, false);

                data = Source.GetComponentData(x, y, gComponent);
                var g = Binary.ReadBits(data.Array, data.Offset, gComponent.Size, false);

                data = Source.GetComponentData(x, y, bComponent);
                var b = Binary.ReadBits(data.Array, data.Offset, bComponent.Size, false);

                double yValue = 0.299 * r + 0.587 * g + 0.114 * b;
                double uValue = -0.14713 * r - 0.28886 * g + 0.436 * b;
                double vValue = 0.615 * r - 0.51498 * g - 0.10001 * b;

                byte yByte = (byte)Math.Max(0, Math.Min(255, yValue));
                byte uByte = (byte)Math.Max(0, Math.Min(255, uValue + 128));
                byte vByte = (byte)Math.Max(0, Math.Min(255, vValue + 128));

                Binary.WriteBits(yComponentData.Array, yComponentData.Offset, yComponent.Size, yByte, false);
                Destination.SetComponentData(x, y, yComponentIndex, yComponentData);

                Binary.WriteBits(uComponentData.Array, uComponentData.Offset, uComponent.Size, uByte, false);
                Destination.SetComponentData(x, y, uComponentIndex, uComponentData);

                Binary.WriteBits(vComponentData.Array, vComponentData.Offset, vComponent.Size, vByte, false);
                Destination.SetComponentData(x, y, vComponentIndex, vComponentData);
            }
        }
    }
}
