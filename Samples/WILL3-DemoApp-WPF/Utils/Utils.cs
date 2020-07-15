using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Resources;

using Wacom.Ink.Rendering;

namespace Wacom
{
    public static class Utils
    {
        static readonly DateTime s_epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long GetTimestampMicroseconds()
        {
            long usec = (long)(1000 * DateTime.Now.ToUniversalTime().Subtract(s_epoch).TotalMilliseconds);
            return usec;
        }

        /// <summary>
        /// Loads bitmap pixel data from app resources
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static PixelData GetPixelData(Uri uri)
        {
            BitmapImage original = new BitmapImage(uri);
            return GetPixelData(original);
        }

        /// <summary>
        /// Loads image file data from app resources
        /// </summary>
        public static byte[] GetImageFileData(Uri uri)
        {
            StreamResourceInfo sri = Application.GetResourceStream(uri);
            if (sri != null)
            {
                using (Stream s = sri.Stream)
                {
                    byte[] data = new byte[s.Length];
                    s.Read(data, 0, (int)s.Length);
                    return data;
                }
            }
            return null;
        }

        public static PixelData GetPixelData(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();

            return GetPixelData(image);
        }

        private static PixelData GetPixelData(BitmapImage original)
        {
            BitmapSource source = original;

            if (original.Format != PixelFormats.Pbgra32)
            {
                source = new FormatConvertedBitmap(original, PixelFormats.Pbgra32, null, 0.0);
            }

            int stride = source.PixelWidth * 4;
            int size = source.PixelHeight * stride;
            byte[] pixels = new byte[size];
            source.CopyPixels(pixels, stride, 0);

            return new PixelData(pixels, (uint)source.PixelWidth, (uint)source.PixelHeight);
        }

    }

}
