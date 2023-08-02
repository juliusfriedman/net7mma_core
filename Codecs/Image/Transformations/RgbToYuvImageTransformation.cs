using Media.Codec;
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
                byte r = Source.GetPixelComponent(x, y, ImageFormat.RedChannelId)[0];
                byte g = Source.GetPixelComponent(x, y, ImageFormat.GreenChannelId)[0];
                byte b = Source.GetPixelComponent(x, y, ImageFormat.BlueChannelId)[0];

                double yValue = 0.299 * r + 0.587 * g + 0.114 * b;
                double uValue = -0.14713 * r - 0.28886 * g + 0.436 * b;
                double vValue = 0.615 * r - 0.51498 * g - 0.10001 * b;

                byte yByte = (byte)Math.Max(0, Math.Min(255, yValue));
                byte uByte = (byte)Math.Max(0, Math.Min(255, uValue + 128));
                byte vByte = (byte)Math.Max(0, Math.Min(255, vValue + 128));

                var temp = new Common.MemorySegment(1);
                temp[0] = yByte;
                Destination.SetComponentData(x, y, ImageFormat.LumaChannelId, temp);
                temp[0] = uByte;
                Destination.SetComponentData(x, y, ImageFormat.ChromaMajorChannelId, temp);
                temp[0] = vByte;
                Destination.SetComponentData(x, y, ImageFormat.ChromaMinorChannelId, temp);
            }
        }
    }
}
