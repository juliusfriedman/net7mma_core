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
    private static bool IsYuvImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.Components.Any(c => c.Id == ImageFormat.LumaChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.ChromaMajorChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.ChromaMinorChannelId);
    }

    // Check if the image format is an RGB format
    private static bool IsRgbImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.Components.Any(c => c.Id == ImageFormat.RedChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.GreenChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.BlueChannelId);
    }

    public override void Transform()
    {
        // Loop through each pixel in the source image
        for (int y = 0; y < Source.Height; y++)
        {
            for (int x = 0; x < Source.Width; x++)
            {
                // Read the YUV components for the current pixel
                var component = Source.ImageFormat.GetComponentById(ImageFormat.LumaChannelId);
                var data = Source.GetComponentData(x, y, component);
                var yValue = Binary.ReadBits(data.Array, data.Offset, component.Size, false);

                component = Source.ImageFormat.GetComponentById(ImageFormat.ChromaMajorChannelId);
                data = Source.GetComponentData(x, y, component);
                var uValue = Binary.ReadBits(data.Array, data.Offset, component.Size, false);

                component = Source.ImageFormat.GetComponentById(ImageFormat.ChromaMinorChannelId);
                data = Source.GetComponentData(x, y, component);
                var vValue = Binary.ReadBits(data.Array, data.Offset, component.Size, false);

                // Convert YUV to RGB values
                double r = yValue + 1.13983 * (vValue - 128);
                double g = yValue - 0.39465 * (uValue - 128) - 0.58060 * (vValue - 128);
                double b = yValue + 2.03211 * (uValue - 128);

                // Clamp the RGB values to the range [0, 255]
                byte rByte = (byte)Math.Max(0, Math.Min(255, r));
                byte gByte = (byte)Math.Max(0, Math.Min(255, g));
                byte bByte = (byte)Math.Max(0, Math.Min(255, b));

                // Write the RGB components to the destination image
                component = Destination.ImageFormat.GetComponentById(ImageFormat.RedChannelId);
                data = new Common.MemorySegment(component.Length);
                Binary.WriteBits(data.Array, data.Offset, component.Length, rByte, false);
                Destination.SetComponentData(x, y, ImageFormat.RedChannelId, data);

                component = Destination.ImageFormat.GetComponentById(ImageFormat.GreenChannelId);
                data = new Common.MemorySegment(component.Length);
                Binary.WriteBits(data.Array, data.Offset, component.Length, gByte, false);
                Destination.SetComponentData(x, y, ImageFormat.GreenChannelId, data);

                component = Destination.ImageFormat.GetComponentById(ImageFormat.BlueChannelId);
                data = new Common.MemorySegment(component.Length);
                Binary.WriteBits(data.Array, data.Offset, component.Length, bByte, false);
                Destination.SetComponentData(x, y, ImageFormat.BlueChannelId, data);
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
    private static bool IsYuvImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.Components.Any(c => c.Id == ImageFormat.LumaChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.ChromaMajorChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.ChromaMinorChannelId);
    }

    // Check if the image format is an RGB format
    private static bool IsRgbImage(ImageFormat format)
    {
        return format.Components.Length >= 3 &&
               format.Components.Any(c => c.Id == ImageFormat.RedChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.GreenChannelId) &&
               format.Components.Any(c => c.Id == ImageFormat.BlueChannelId);
    }

    public override void Transform()
    {
        int width = Source.Width;
        int height = Source.Height;

        // Prepare Vector<float> constants for conversion formulas
        Vector<float> vector128 = new Vector<float>(128f);
        Vector<float> vector1_13983 = new Vector<float>(1.13983f);
        Vector<float> vector0_39465 = new Vector<float>(0.39465f);
        Vector<float> vector0_58060 = new Vector<float>(0.58060f);
        Vector<float> vector2_03211 = new Vector<float>(2.03211f);

        // Process the image in 4-pixel blocks (assuming YUV format)
        int blockSize = 4;
        int blockWidth = width / blockSize * blockSize;

        var rComponentIndex = Destination.GetComponentIndex(ImageFormat.RedChannelId);
        var gComponentIndex = Destination.GetComponentIndex(ImageFormat.GreenChannelId);
        var bComponentIndex = Destination.GetComponentIndex(ImageFormat.BlueChannelId);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < blockWidth; x += blockSize)
            {
                // Load the pixel data into Vector<float> arrays
                Vector<byte> yVector = Source.GetComponentVector(x + 0, y, ImageFormat.LumaChannelId); //new Vector<byte>(Source.GetComponentData(x + 0, y, ImageFormat.LumaChannelId).ToSpan());
                Vector<byte> uVector = Source.GetComponentVector(x + 1, y, ImageFormat.ChromaMajorChannelId);//new Vector<byte>(Source.GetComponentData(x + 1, y, ImageFormat.ChromaMajorChannelId).ToSpan());
                Vector<byte> vVector = Source.GetComponentVector(x + 2, y, ImageFormat.ChromaMinorChannelId);//new Vector<byte>(Source.GetComponentData(x + 2, y, ImageFormat.ChromaMinorChannelId).ToSpan());

                // Convert YUV to RGB using vectorized calculations
                Vector<float> yFloat = (Vector<float>)yVector;
                Vector<float> uFloat = (Vector<float>)uVector - vector128;
                Vector<float> vFloat = (Vector<float>)vVector - vector128;

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
                Destination.SetComponentData(x + 0, y, rComponentIndex, rByte);
                Destination.SetComponentData(x + 1, y, gComponentIndex, gByte);
                Destination.SetComponentData(x + 2, y, bComponentIndex, bByte);
            }

            // Process any remaining pixels (not in 4-pixel blocks)
            for (int x = blockWidth; x < width; x++)
            {
                // Read the YUV components for the current pixel
                var component = Source.ImageFormat.GetComponentById(ImageFormat.LumaChannelId);
                var data = Source.GetComponentData(x, y, component);
                var yValue = Binary.ReadBits(data.Array, data.Offset, component.Size, false);

                component = Source.ImageFormat.GetComponentById(ImageFormat.ChromaMajorChannelId);
                data = Source.GetComponentData(x, y, component);
                var uValue = Binary.ReadBits(data.Array, data.Offset, component.Size, false);

                component = Source.ImageFormat.GetComponentById(ImageFormat.ChromaMinorChannelId);
                data = Source.GetComponentData(x, y, component);
                var vValue = Binary.ReadBits(data.Array, data.Offset, component.Size, false);

                // Convert YUV to RGB values
                double r = yValue + 1.13983 * (vValue - 128);
                double g = yValue - 0.39465 * (uValue - 128) - 0.58060 * (vValue - 128);
                double b = yValue + 2.03211 * (uValue - 128);

                // Clamp the RGB values to the range [0, 255]
                byte rByte = (byte)Math.Max(0, Math.Min(255, r));
                byte gByte = (byte)Math.Max(0, Math.Min(255, g));
                byte bByte = (byte)Math.Max(0, Math.Min(255, b));

                // Write the RGB components to the destination image
                component = Destination.ImageFormat.GetComponentById(ImageFormat.RedChannelId);
                data = new Common.MemorySegment(component.Length);
                Binary.WriteBits(data.Array, data.Offset, component.Length, rByte, false);
                Destination.SetComponentData(x, y, ImageFormat.RedChannelId, data);

                component = Destination.ImageFormat.GetComponentById(ImageFormat.GreenChannelId);
                data = new Common.MemorySegment(component.Length);
                Binary.WriteBits(data.Array, data.Offset, component.Length, gByte, false);
                Destination.SetComponentData(x, y, ImageFormat.GreenChannelId, data);

                component = Destination.ImageFormat.GetComponentById(ImageFormat.BlueChannelId);
                data = new Common.MemorySegment(component.Length);
                Binary.WriteBits(data.Array, data.Offset, component.Length, bByte, false);
                Destination.SetComponentData(x, y, ImageFormat.BlueChannelId, data);
            }
        }
    }
}
