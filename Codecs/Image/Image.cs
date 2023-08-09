using Media.Codec;
using Media.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Media.Codecs.Image
{
    public class Image : Media.Codec.MediaBuffer
    {
        const float DefaultDpi = 96.0f;

        #region Statics

        //static Image Crop(Image source)

        internal static int CalculateSize(ImageFormat format, int width, int height)
        {
            //The total size in bytes
            int totalSize = 0;

            //Iterate each component in the ColorSpace
            for (int i = 0, ie = format.Components.Length; i <ie ; ++i)
            {
                //Increment the total size in bytes by calculating the size in bytes of that plane using the ColorSpace information
                totalSize += (width >> format.Widths[i]) * (height >> format.Heights[i]);
            }

            //Return the amount of bytes
            return totalSize;
        }

        #endregion

        #region Fields

        public readonly int Width;

        public readonly int Height;
        
        #endregion

        #region Constructor

        //Needs a subsampling...

        public Image(ImageFormat format, int width, int height)
            : base(format, CalculateSize(format, width, height))
        {
            Width = width;

            Height = height;
        }

        public Image(ImageFormat format, int width, int height, byte[] data)
            : base(format, new Common.MemorySegment(data))
        {
            Width = width;

            Height = height;
        }

        #endregion

        #region Properties

        //Should be Vector<byte>?

        //Assumes component order
        public IEnumerable<Common.MemorySegment> this[int x, int y]
        {
            get
            {
                if (x < 0 || y < 0 || x > Width || y > Height)
                    yield break;

                //Loop each component and return the segment which corresponds to the data at the offset for that component
                for (int c = 0, ce = ImageFormat.Components.Length; c < ce; ++c)
                    yield return new Common.MemorySegment(Data.Array, Data.Offset + CalculateComponentDataOffset(x, y, c), ImageFormat[c].Length);
            }
            set
            {
                if (x < 0 || y < 0 || x > Width || y > Height)
                    return;

                int componentIndex = 0;

                foreach(var componentData in value)
                    SetComponentData(x, y, componentIndex++, componentData);
            }
        }

        public ImageFormat ImageFormat { get { return MediaFormat as ImageFormat; } }

        public int Planes { get { return MediaFormat.Components.Length; } }

        #endregion

        #region Methods

        public void SaveBitmap(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            int width = Width;
            int height = Height;

            //Should be a 4CC indicating the image but...
            var compressionFormat = 0;// Binary.Read32(ImageFormat.Components.Select(c => (byte)char.ToUpper((char)c.Id)).ToArray(), 0, Binary.IsBigEndian);

            // Convert pixels to meters: 1 inch = 0.0254 meters
            float horizontalResolutionMeters = width / DefaultDpi * 0.0254f;
            float verticalResolutionMeters = height / DefaultDpi * 0.0254f;

            // Convert meters to pixels per meter
            int xpelsPerMeter = (int)Math.Round(1.0f / horizontalResolutionMeters);
            int ypelsPerMeter = (int)Math.Round(1.0f / verticalResolutionMeters);

            // Create a new BitmapInfoHeader based on the ImageFormat
            BitmapInfoHeader header = new BitmapInfoHeader(width, height, (short)ImageFormat.Components.Length, (short)ImageFormat.Size, compressionFormat, Data.Array.Length, xpelsPerMeter, ypelsPerMeter, 0, 0);
            SaveBitmap(header, stream);
        }

        public void SaveBitmap(BitmapInfoHeader header, Stream stream)
        {
            int fileSize = 54 + header.Count + Data.Array.Length;

            // BMP file header
            byte[] fileHeader = new byte[14]
            {
                0x42, 0x4D,                       // "BM" - BMP file identifier
                (byte)(fileSize & 0xFF),          // File size (low byte)
                (byte)((fileSize >> 8) & 0xFF),   // File size
                (byte)((fileSize >> 16) & 0xFF),  // File size
                (byte)((fileSize >> 24) & 0xFF),  // File size (high byte)
                0x00, 0x00,                       // Reserved
                0x00, 0x00,                       // Reserved
                0x36, 0x00, 0x00, 0x00            // Offset of the image data (54 bytes)
            };

            // Write the BMP file header to the stream
            stream.Write(fileHeader, 0, fileHeader.Length);
            stream.Write(header.Array, header.Offset, header.Count);
            stream.Write(Data.Array, Data.Offset, Data.Count);
        }       

        public int PlaneWidth(int plane)
        {
            if (plane >= MediaFormat.Components.Length) return -1;

            return Width >> ImageFormat.Widths[plane];
        }

        public int PlaneHeight(int plane)
        {
            if (plane >= MediaFormat.Components.Length) return -1;

            return Height >> ImageFormat.Heights[plane];
        }

        /// <summary>
        /// Gets the amount of bits in the given plane
        /// </summary>
        /// <param name="plane"></param>
        /// <returns></returns>
        public int PlaneSize(int plane)
        {
            if (plane >= MediaFormat.Components.Length) return -1;

            return (PlaneWidth(plane) + PlaneHeight(plane)) * ImageFormat.Size;
        }

        /// <summary>
        /// Gets the amount of bytes in the given plane.
        /// </summary>
        /// <param name="plane"></param>
        /// <returns></returns>
        public int PlaneLength(int plane)
        {
            if (plane >= MediaFormat.Components.Length) return -1;

            return Common.Binary.BitsToBytes(PlaneSize(plane));
        }

        /// <summary>
        /// Calculates the byte offset to component/channel data
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="componentIndex"></param>
        /// <returns></returns>
        public int CalculateComponentDataOffset(int x, int y, int componentIndex)
        {
            if (componentIndex < 0 || componentIndex >= ImageFormat.Components.Length)
                return -1;

            int offset = 0;

            var component = ImageFormat.Components[componentIndex];

            switch (DataLayout)
            {
                case Media.Codec.DataLayout.Planar:
                    int widthSampling = ImageFormat.Widths[componentIndex];
                    int heightSampling = ImageFormat.Heights[componentIndex];

                    int planeWidth = Width >> widthSampling;
                    int planeX = x >> widthSampling;
                    int planeY = y >> heightSampling;

                    offset += (planeY * planeWidth + planeX) * component.Length;
                    break;

                case Media.Codec.DataLayout.SemiPlanar:
                    int yPlaneWidth = PlaneWidth(componentIndex);
                    int yPlaneHeight = PlaneHeight(componentIndex);
                    // Y plane
                    if (component.Id == ImageFormat.LumaChannelId)
                    {
                        offset += (y * yPlaneWidth + x) * component.Length;
                        break;
                    }
                    else// UV plane (interleaved
                    {
                        int uvOffset = yPlaneWidth * yPlaneHeight;
                        int uvComponentIndex = y / 2 * (yPlaneWidth / 2) + x / 2;
                        offset += uvOffset + uvComponentIndex * component.Length;
                        break;
                    }
                case Media.Codec.DataLayout.Packed:
                    int componentDataIndex = y * Width + x;
                    int bytesPerChannel = ImageFormat.Length; // Number of bytes per channel (e.g., 3 for RGB24)
                    offset += componentDataIndex * bytesPerChannel;
                    break;
            }

            return offset;
        }
        
        //Gets all component samples for the given coordinate into a SegmentStream (not really useful besides saving on some allocation)

        public SegmentStream GetSampleData(int x, int y)
        {
            var result = new SegmentStream();
            foreach (var component in ImageFormat.Components)
                result.AddMemory(GetComponentData(x, y, component));
            return result;
        }

        // Get the value of a specific component at the given (x, y) coordinates

        public MemorySegment GetComponentData(int x, int y, MediaComponent component)
        {
            int componentIndex = GetComponentIndex(component);

            if (componentIndex < 0) return MemorySegment.Empty;
            //throw new ArgumentException("Invalid component specified.");

            int offset = CalculateComponentDataOffset(x, y, componentIndex);

            if (offset < 0) return MemorySegment.Empty;
            //throw new IndexOutOfRangeException("Coordinates are outside the bounds of the image.");

            return new MemorySegment(Data.Array, offset, component.Length);
        }

        public MemorySegment GetComponentData(int x, int y, byte componentId) => GetComponentData(x, y, ImageFormat.GetComponentById(componentId));

        public Vector<byte> GetComponentVector(int x, int y, byte componentId) => GetComponentVector(x, y, GetComponentIndex(componentId));

        public Vector<byte> GetComponentVector(int x, int y, int componentIndex)
        {
            int offset = CalculateComponentDataOffset(x, y, componentIndex);
            offset -= offset % Vector<byte>.Count; // Align the offset to vector size
            return new Vector<byte>(Data.Array, Data.Offset + offset);
        }

        // Set the value of a specific component at the given (x, y) coordinates

        public void SetComponentData(int x, int y, int componentIndex, MemorySegment data)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return; // or throw an exception

            int offset = CalculateComponentDataOffset(x, y, componentIndex);
            if (offset < 0)
                return; // or throw an exception

            Buffer.BlockCopy(data.Array, data.Offset, Data.Array, Data.Offset + offset, data.Count);
        }

        public void SetComponentVector(int x, int y, int componentIndex, Vector<byte> componentVector)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return; // or throw an exception

            int offset = CalculateComponentDataOffset(x, y, componentIndex);
            if (offset < 0)
                return; // or throw an exception

            //int vectorOffset = offset - (offset % Vector<byte>.Count);
            componentVector.CopyTo(new Span<byte>(Data.Array, Data.Offset + offset, Vector<byte>.Count));
        }

        public void SetComponentData(int x, int y, byte componentId, MemorySegment data) => SetComponentData(x, y, GetComponentIndex(componentId), data);

        public void Fill(byte value) => Array.Fill(Data.Array, value, Data.Offset, Data.Count);

        public void FillVector(byte value)
        {
            var filledVector = new Vector<byte>(value);
            for (int i = 0; i < Data.Count; i += Vector<byte>.Count)
                filledVector.CopyTo(new Span<byte>(Data.Array, Data.Offset + i, Vector<byte>.Count));
        }

        #endregion
    }
}


namespace Media.UnitTests
{
    /// <summary>
    /// Provides tests which ensure the logic of the Image class is correct
    /// </summary>
    internal class ImageUnitTests
    {
        public static void TestConstructor()
        {
            using (Media.Codecs.Image.Image image = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8), 1, 1))
            {
                if (image.SampleCount != 1) throw new System.InvalidOperationException();

                if (image.Data.Count != image.Width * image.Height * image.MediaFormat.Length) throw new System.InvalidOperationException();
            }
        }

        public static void Test_GetComponentData_SetComponentData()
        {
            System.DateTime start = System.DateTime.UtcNow, end;

            using (var image = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8), 50, 50))
            {
                for (int x = 0; x < image.Width; x++)
                {
                    for(int y = 0; y < image.Height; y++)
                    {
                        for (int c = 0; c < image.ImageFormat.Length; c++)
                        {
                            var component = image.ImageFormat[c];

                            var data = image.GetComponentData(x, y, component);

                            if (data.Count != component.Length) throw new InvalidOperationException();

                            Array.Fill(data.Array, byte.MaxValue);

                            image.SetComponentData(x, y, c, data);
                        }
                    }
                }

                end = System.DateTime.UtcNow;

                System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms for image width,height=" + image.Width + "," + image.Height);

                if (image.Data.Array.Any(b => b != byte.MaxValue)) throw new Exception("Did not set Component data");
            }
        }        

        public static void Test_Get_Set_Indexer()
        {
            System.DateTime start = System.DateTime.UtcNow, end;

            using (var image = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8), 50, 50))
            {
                for (int x = 0; x < image.Width; x++)
                {
                    for (int y = 0; y < image.Height; y++)
                    {
                        var data = image[x, y];

                        foreach (var componentData in data)
                            Array.Fill(componentData.Array, byte.MaxValue);

                        image[x, y] = data; 
                    }
                }

                end = System.DateTime.UtcNow;

                System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms for image width,height=" + image.Width + "," + image.Height);

                if (image.Data.Array.Any(b => b != byte.MaxValue)) throw new Exception("Did not set Component data");
            }
        }

        public static void TestSave()
        {
            using (var image = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8), 696, 564))
            {
                Array.Fill(image.Data.Array, byte.MaxValue);

                using (var outputBmpStream = new System.IO.FileStream("output_rgb.bmp", FileMode.OpenOrCreate))
                {
                    image.SaveBitmap(outputBmpStream);
                }
            }


            using (var image = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGBA(8), 696, 564))
            {
                Array.Fill(image.Data.Array, byte.MaxValue);

                using (var outputBmpStream = new System.IO.FileStream("output_rgba.bmp", FileMode.OpenOrCreate))
                {
                    image.SaveBitmap(outputBmpStream);
                }
            }
        }

        public void Test_CalculateComponentDataOffset_PlanarLayout()
        {
            var imageFormat = Media.Codecs.Image.ImageFormat.RGB(8, Binary.ByteOrder.Little, DataLayout.Planar);
            var image = new Codecs.Image.Image(imageFormat, 640, 480);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    for (int componentIndex = 0; componentIndex < imageFormat.Length; componentIndex++)
                    {
                        int widthSampling = imageFormat.Widths[componentIndex];
                        int heightSampling = imageFormat.Heights[componentIndex];

                        int planeWidth = image.Width >> widthSampling;
                        int planeX = x >> widthSampling;
                        int planeY = y >> heightSampling;

                        int expectedOffset = (planeY * planeWidth + planeX) * imageFormat.Components[componentIndex].Length;

                        int calculatedOffset = image.CalculateComponentDataOffset(x, y, componentIndex);

                        if (expectedOffset != calculatedOffset) throw new InvalidOperationException();
                    }
                }
            }
        }

        public void Test_CalculateComponentDataOffset_SemiPlanarLayout()
        {
            var imageFormat = Media.Codecs.Image.ImageFormat.YUV(8, Binary.ByteOrder.Little, DataLayout.SemiPlanar);
            var image = new Codecs.Image.Image(imageFormat, 640, 480);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    for (int componentIndex = 0; componentIndex < imageFormat.Length; componentIndex++)
                    {
                        int expectedOffset;
                        if (componentIndex == 0) // Y component
                        {
                            expectedOffset = y * image.Width + x;
                        }
                        else // UV components
                        {
                            expectedOffset = (image.Width * image.Height) + ((y / 2) * (image.Width / 2) + (x / 2)) * imageFormat.Components[componentIndex].Length;
                        }

                        int calculatedOffset = image.CalculateComponentDataOffset(x, y, componentIndex);

                        if (expectedOffset != calculatedOffset) throw new InvalidOperationException();
                    }
                }
            }
        }

        public void Test_CalculateComponentDataOffset_PackedLayout()
        {
            var imageFormat = Media.Codecs.Image.ImageFormat.BGR(8);
            var image = new Codecs.Image.Image(imageFormat, 640, 480);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    for (int componentIndex = 0; componentIndex < imageFormat.Length; componentIndex++)
                    {
                        int expectedOffset = (y * image.Width + x) * imageFormat.Length;

                        int calculatedOffset = image.CalculateComponentDataOffset(x, y, componentIndex);

                        if (expectedOffset != calculatedOffset) throw new InvalidOperationException();
                    }
                }
            }
        }

        public static void TestConversionRGB()
        {
            int testHeight = 1920, testWidth = 1080;

            System.DateTime start = System.DateTime.UtcNow, end;

            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8), testWidth, testHeight))
            {
                if (rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new Codecs.Image.ImageFormat(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //ImageFormat could be given directly to constructor here

                //Create the destination image
                using (Media.Codecs.Image.Image yuvImage = new Codecs.Image.Image(Yuv420P, testWidth, testHeight))
                {

                    //Cache the data of the source before transformation
                    byte[] left = rgbImage.Data.ToArray(), right;

                    //Transform RGB to YUV

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.RgbToYuvImageTransformation(rgbImage, yuvImage))
                    {
                        start = System.DateTime.UtcNow;

                        it.Transform();

                        end = System.DateTime.UtcNow;

                        System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                        //Yuv Data
                        //left = dest.Data.ToArray();
                    }

                    //Transform YUV to RGB

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.YuvToRgbTransformation(yuvImage, rgbImage))
                    {
                        start = System.DateTime.UtcNow;

                        it.Transform();

                        end = System.DateTime.UtcNow;

                        System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                        //Rgb Data
                        right = rgbImage.Data.ToArray();
                    }

                    //Compare the two sequences
                    if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();
                }

                //Done with the format.
                Yuv420P = null;
            }
        }

        public static void TestVectorizedConversionRGB()
        {
            int testHeight = 1920, testWidth = 1080;

            System.DateTime start = System.DateTime.UtcNow, end;

            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8), testWidth, testHeight))
            {
                if (rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new Codecs.Image.ImageFormat(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //ImageFormat could be given directly to constructor here

                //Create the destination image
                using (Media.Codecs.Image.Image yuvImage = new Codecs.Image.Image(Yuv420P, testWidth, testHeight))
                {

                    //Cache the data of the source before transformation
                    byte[] left = rgbImage.Data.ToArray(), right;

                    //Transform RGB to YUV

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.VectorizedRgbToYuvImageTransformation(rgbImage, yuvImage))
                    {
                        start = System.DateTime.UtcNow;

                        it.Transform();

                        end = System.DateTime.UtcNow;

                        System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                        //Yuv Data
                        //left = dest.Data.ToArray();
                    }

                    //Transform YUV to RGB

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.VectorizedYuvToRgbTransformation(yuvImage, rgbImage))
                    {
                        start = System.DateTime.UtcNow;

                        it.Transform();

                        end = System.DateTime.UtcNow;

                        System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                        //Rgb Data
                        right = rgbImage.Data.ToArray();
                    }

                    //Compare the two sequences
                    if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();
                }

                //Done with the format.
                Yuv420P = null;
            }
        }

        //Averages around 100 msec, 0.1 sec, still 60 times would be 600 msec or .6 seconds, just fast enough
        public static void TestUnsafeConversionRGB()
        {
            int testHeight = 1920, testWidth = 1080;

            System.DateTime start = System.DateTime.UtcNow, end;

            Codecs.Image.Image yuvImage;

            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8), testWidth, testHeight))
            {
                if (rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new Codecs.Image.ImageFormat(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //ImageFormat could be given directly to constructor here

                //Create the destination image

                //Cache the data of the source before transformation
                byte[] left = rgbImage.Data.ToArray(), right;

                //Transform RGB to YUV

                start = System.DateTime.UtcNow;

                unsafe
                {
                    fixed (byte* ptr = rgbImage.Data.Array)
                    {
                        yuvImage = new Codecs.Image.Image(Yuv420P, testWidth, testHeight, Media.Codecs.Image.ColorConversions.RGBToYUV420Managed(rgbImage.Width, rgbImage.Height, (System.IntPtr)ptr));
                    }

                }

                end = System.DateTime.UtcNow;

                System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                //Transform YUV to RGB

                start = System.DateTime.UtcNow;

                //it.Transform();

                Media.Codecs.Image.ColorConversions.YUV2RGBManaged(yuvImage.Data.Array, rgbImage.Data.Array, rgbImage.Width >> 1, rgbImage.Height >> 1);

                end = System.DateTime.UtcNow;

                System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                //Rgb Data
                right = rgbImage.Data.ToArray();

                //Compare the two sequences
                //if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();

                //Done with the format.
                Yuv420P = null;
            }
        }

        public static void TestConversionARGB()
        {
            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.ARGB(8), 8, 8))
            {
                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new Codecs.Image.ImageFormat(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                if (false == rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be true");

                //ImageFormat be given directly to constructor here

                //Create the destination image
                using (Media.Codecs.Image.Image yuvImage = new Codecs.Image.Image(Yuv420P, 8, 8))
                {

                    //Cache the data of the source before transformation
                    byte[] left = rgbImage.Data.ToArray(), right;

                    //Transform RGB to YUV

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.RgbToYuvImageTransformation(rgbImage, yuvImage))
                    {
                        it.Transform();

                        //Yuv Data
                        //left = dest.Data.ToArray();
                    }

                    //Transform YUV to RGB

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.YuvToRgbTransformation(yuvImage, rgbImage))
                    {
                        it.Transform();

                        //Rgb Data
                        right = rgbImage.Data.ToArray();
                    }

                    //Compare the two sequences
                    if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();
                }

                //Done with the format.
                Yuv420P = null;
            }
        }

        public static void TestConversionRGBA()
        {
            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGBA(8), 8, 8))
            {
                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new Codecs.Image.ImageFormat(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                if (false == rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be true");

                //ImageFormat could be given directly to constructor here

                //Create the destination image
                using (Media.Codecs.Image.Image yuvImage = new Codecs.Image.Image(Yuv420P, 8, 8))
                {

                    //Cache the data of the source before transformation
                    byte[] left = rgbImage.Data.ToArray(), right;

                    //Transform RGB to YUV

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.RgbToYuvImageTransformation(rgbImage, yuvImage))
                    {
                        it.Transform();

                        //Yuv Data
                        //left = dest.Data.ToArray();
                    }

                    //Transform YUV to RGB

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.YuvToRgbTransformation(yuvImage, rgbImage))
                    {
                        it.Transform();

                        //Rgb Data
                        right = rgbImage.Data.ToArray();
                    }

                    //Compare the two sequences
                    if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();
                }

                //Done with the format.
                Yuv420P = null;
            }
        }

        public static void TestConversionRGAB()
        {

            Codecs.Image.ImageFormat weird = new Codecs.Image.ImageFormat(Common.Binary.ByteOrder.Little, Codec.DataLayout.Packed, new Codec.MediaComponent[]
            {
                new Codec.MediaComponent((byte)'r', 10),
                new Codec.MediaComponent((byte)'g', 10),
                new Codec.MediaComponent((byte)'a', 2),
                new Codec.MediaComponent((byte)'b', 10),
                
            });

            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new Codecs.Image.Image(weird, 8, 8))
            {
                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new Codecs.Image.ImageFormat(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                if (false == rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be true");

                if (rgbImage.ImageFormat.AlphaComponent.Size != 2) throw new System.Exception("AlphaComponent.Size should be 2 (bits)");

                //ImageFormat could be given directly to constructor here

                //Create the destination image
                using (Media.Codecs.Image.Image yuvImage = new Codecs.Image.Image(Yuv420P, 8, 8))
                {

                    //Cache the data of the source before transformation
                    byte[] left = rgbImage.Data.ToArray(), right;

                    //Transform RGB to YUV

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.RgbToYuvImageTransformation(rgbImage, yuvImage))
                    {
                        it.Transform();

                        //Yuv Data
                        //left = dest.Data.ToArray();
                    }

                    //Transform YUV to RGB

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.YuvToRgbTransformation(yuvImage, rgbImage))
                    {
                        it.Transform();

                        //Rgb Data
                        right = rgbImage.Data.ToArray();
                    }

                    //Compare the two sequences
                    if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();
                }

                //Done with the format.
                Yuv420P = null;
            }
        }

        public static void TestFillVector()
        {
            System.DateTime start = System.DateTime.UtcNow, end;

            Media.Codecs.Image.ImageFormat Yuv420P = new Codecs.Image.ImageFormat(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

            using (var image = new Codecs.Image.Image(Yuv420P, 32, 32))
            {
                image.FillVector(byte.MaxValue);

                end = System.DateTime.UtcNow;

                System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms for image width,height=" + image.Width + "," + image.Height);

                if (image.Data.Array.Any(b => b != byte.MaxValue)) throw new InvalidOperationException("Did not set Component data (Vector)");
            }
        }

        //This should work even though its not the correct way to loop for vectors
        public static void Test_GetComponentVector_SetComponentVector()
        {
            System.DateTime start = System.DateTime.UtcNow, end;

            var filledVector = new Vector<byte>(byte.MaxValue);

            using (Media.Codecs.Image.ImageFormat Yuv420P = new Codecs.Image.ImageFormat(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 }))
            {
                using (var image = new Codecs.Image.Image(Yuv420P, 32, 32))
                {
                    for (int c = 0; c < image.ImageFormat.Length; c++)
                    {
                        for (int x = 0; x < image.Width; ++x)
                        {
                            for (int y = 0; y < image.Height; ++y)
                            {
                                var offset = image.CalculateComponentDataOffset(x, y, c);
                                Console.WriteLine($"{x},{y},{c} = {offset}");

                                // Set the modified data back to the image
                                image.SetComponentVector(x, y, c, filledVector);

                                var vector = image.GetComponentVector(x, y, c);

                                if (!vector.Equals(filledVector)) throw new InvalidOperationException();
                            }
                        }
                    }

                    end = System.DateTime.UtcNow;

                    System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms for image width,height=" + image.Width + "," + image.Height);

                    if (image.Data.Array.Any(b => b != byte.MaxValue)) throw new InvalidOperationException("Did not set Component data (Vector)");
                }
            }
        }
    }
}