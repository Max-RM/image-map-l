using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;

namespace ImageMap.WinForms
{
    internal static class Assets
    {
        private static readonly Assembly Assembly = typeof(Assets).Assembly;

        public static readonly Image World = Load("world.png");
        public static readonly Image Map = Load("map.png");
        public static readonly Image ItemFrame = Load("item_frame.png");
        public static readonly Image GlowItemFrame = Load("glow_item_frame.png");
        public static readonly Image Warning = Load("warning.png");
        public static readonly Image Checkmark = Load("checkmark.png");
        public static readonly Image MapBackground = Load("map_background.png");

        private static Image Load(string name)
        {
            using var stream = Assembly.GetManifestResourceStream("ImageMap.WinForms.Assets." + name);
            if (stream == null)
                throw new InvalidOperationException("Missing embedded asset: " + name);
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }

        public static Image CreateFolderIcon(int width = 24, int height = 18)
        {
            var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            float sx = width / 16f;
            float sy = height / 16f;
            using var back = new SolidBrush(Color.FromArgb(255, 225, 190, 80));
            using var middle = new SolidBrush(Color.FromArgb(255, 236, 200, 96));
            using var front = new SolidBrush(Color.FromArgb(255, 248, 214, 120));
            using var line = new Pen(Color.FromArgb(255, 150, 111, 0), Math.Max(1f, 1f * sx));
            var tab = new[]
            {
                new PointF(0.5f * sx, 3f * sy),
                new PointF(6.5f * sx, 3f * sy),
                new PointF(8f * sx, 5f * sy),
                new PointF(15.5f * sx, 5f * sy),
                new PointF(15.5f * sx, 13.5f * sy),
                new PointF(0.5f * sx, 13.5f * sy)
            };
            graphics.FillPolygon(middle, tab);
            graphics.DrawPolygon(line, tab);
            var body = new[]
            {
                new PointF(1f * sx, 6f * sy),
                new PointF(15f * sx, 6f * sy),
                new PointF(13.5f * sx, 14.5f * sy),
                new PointF(0.5f * sx, 14.5f * sy)
            };
            graphics.FillPolygon(front, body);
            graphics.DrawPolygon(line, body);
            graphics.FillRectangle(back, 1f * sx, 2.5f * sy, 5.5f * sx, 1.5f * sy);
            return bitmap;
        }

        public static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
        {
            var path = new GraphicsPath();
            int diameter = Math.Max(1, radius * 2);
            var arc = new Rectangle(rectangle.Location, new Size(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = rectangle.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rectangle.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rectangle.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
