using System;

namespace Media.Codecs.Image.Transformations;

public class YuvToRgbTransformation : ImageTransformation
{
    public YuvToRgbTransformation(Image source, Image dest, Codec.TransformationQuality quality = Codec.TransformationQuality.None, bool shouldDispose = true)
        : base(source, dest, quality, shouldDispose)
    {
    }

    public override void Transform()
    {
        if (m_Source == null || m_Dest == null)
            throw new InvalidOperationException("Source and destination images must be set.");

        // Assuming the YUV data is stored in the m_Source image and we want to convert it to RGB in m_Dest

        int width = m_Source.Width;
        int height = m_Source.Height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Get YUV components from the source image at (x, y)
                byte yComponent = m_Source.GetPixelComponent(x, y, m_Source.ImageFormat.GetComponentById(ImageFormat.LumaChannelId))[0];
                byte uComponent = m_Source.GetPixelComponent(x, y, m_Source.ImageFormat.GetComponentById(ImageFormat.ChromaMajorChannelId))[0];
                byte vComponent = m_Source.GetPixelComponent(x, y, m_Source.ImageFormat.GetComponentById(ImageFormat.ChromaMinorChannelId))[0];

                // Perform YUV to RGB conversion
                int c = yComponent - 16;
                int d = uComponent - 128;
                int e = vComponent - 128;

                int r = Common.Binary.Clamp((298 * c + 409 * e + 128) >> 8, 0, 255);
                int g = Common.Binary.Clamp((298 * c - 100 * d - 208 * e + 128) >> 8, 0, 255);
                int b = Common.Binary.Clamp((298 * c + 516 * d + 128) >> 8, 0, 255);

                // Set the RGB components in the destination image at (x, y)
                var temp = new Common.MemorySegment(1);
                temp[0] = (byte)r;
                m_Dest.SetComponentData(x, y, ImageFormat.RedChannelId, temp);
                temp[0] = (byte)g;
                m_Dest.SetComponentData(x, y, ImageFormat.GreenChannelId, temp);
                temp[0] = (byte)b;
                m_Dest.SetComponentData(x, y, ImageFormat.BlueChannelId, temp);
            }
        }
    }
}
