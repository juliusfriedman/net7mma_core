using Media.Common;
using System;
using System.Linq;

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
