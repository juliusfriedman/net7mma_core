using Media.Common;
using System;
using System.Linq;

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
        for (int y = 0; y < Source.Height; y++)
        {
            for (int x = 0; x < Source.Width; x++)
            {
                var component = Source.ImageFormat.GetComponentById(ImageFormat.RedChannelId);
                var data = Source.GetComponentData(x, y, component);
                var r = Binary.ReadBits(data.Array, data.Offset, component.Size, false);

                component = Source.ImageFormat.GetComponentById(ImageFormat.GreenChannelId);
                data = Source.GetComponentData(x, y, component);
                var g = Binary.ReadBits(data.Array, data.Offset, component.Size, false);
                
                component = Source.ImageFormat.GetComponentById(ImageFormat.BlueChannelId);
                data = Source.GetComponentData(x, y, component);
                var b = Binary.ReadBits(data.Array, data.Offset, component.Size, false);

                double yValue = 0.299 * r + 0.587 * g + 0.114 * b;
                double uValue = -0.14713 * r - 0.28886 * g + 0.436 * b;
                double vValue = 0.615 * r - 0.51498 * g - 0.10001 * b;

                byte yByte = (byte)Math.Max(0, Math.Min(255, yValue));
                byte uByte = (byte)Math.Max(0, Math.Min(255, uValue + 128));
                byte vByte = (byte)Math.Max(0, Math.Min(255, vValue + 128));

                component = Destination.ImageFormat.GetComponentById(ImageFormat.LumaChannelId);
                data = new Common.MemorySegment(component.Length);
                Binary.WriteBits(data.Array, data.Offset, component.Size, yByte, false);
                Destination.SetComponentData(x, y, ImageFormat.LumaChannelId, data);

                component = Destination.ImageFormat.GetComponentById(ImageFormat.ChromaMajorChannelId);
                data = new Common.MemorySegment(component.Length);
                Binary.WriteBits(data.Array, data.Offset, component.Size, uByte, false);
                Destination.SetComponentData(x, y, ImageFormat.ChromaMajorChannelId, data);

                component = Destination.ImageFormat.GetComponentById(ImageFormat.ChromaMinorChannelId);
                data = new Common.MemorySegment(component.Length);
                Binary.WriteBits(data.Array, data.Offset, component.Size, vByte, false);
                Destination.SetComponentData(x, y, ImageFormat.ChromaMinorChannelId, data);
            }
        }
    }
}
