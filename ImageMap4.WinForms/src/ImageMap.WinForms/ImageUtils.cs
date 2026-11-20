using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using SixLabors.ImageSharp;
using Rgba32 = SixLabors.ImageSharp.PixelFormats.Rgba32;
using SharpImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

namespace ImageMap.WinForms
{
    public static class ImageUtils
    {
        public static unsafe Bitmap ToBitmap(SharpImage image)
        {
            var bmp = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
            var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte* dest = (byte*)data.Scan0;
                int stride = data.Stride;
                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        byte* d = dest + y * stride;
                        for (int x = 0; x < row.Length; x++)
                        {
                            var p = row[x];
                            d[0] = p.B;
                            d[1] = p.G;
                            d[2] = p.R;
                            d[3] = p.A;
                            d += 4;
                        }
                    }
                });
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return bmp;
        }

        public static void SavePng(SharpImage image, string path) => image.SaveAsPng(path);

        public static SharpImage LoadRgba(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            return SixLabors.ImageSharp.Image.Load<Rgba32>(stream);
        }
    }
}
