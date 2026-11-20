using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ImageMap4;

namespace ImageMap.WinForms
{
    public sealed class PreviewForm : Form
    {
        public PreviewForm(Map map)
        {
            Text = "map_" + map.ID;
            Width = 600;
            Height = 600;
            MinimumSize = new Size(240, 240);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10f);
            BackColor = Color.LightGray;

            var image = new PreviewBox(map)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(10)
            };
            var close = new Button
            {
                Text = "Close",
                AutoSize = true,
                Padding = new Padding(30, 3, 30, 3),
                Anchor = AnchorStyles.None
            };
            close.Click += (s, e) => Close();
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(0, 8, 0, 8) };
            bottom.Controls.Add(close);
            close.Location = new Point((bottom.ClientSize.Width - close.Width) / 2, 10);
            bottom.Resize += (s, e) =>
                close.Location = new Point((bottom.ClientSize.Width - close.Width) / 2, (bottom.ClientSize.Height - close.Height) / 2);
            Controls.Add(image);
            Controls.Add(bottom);
            AcceptButton = close;
        }

        private sealed class PreviewBox : Control
        {
            private readonly Map _map;
            private readonly Bitmap? _bitmap;

            public PreviewBox(Map map)
            {
                _map = map;
                DoubleBuffered = true;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                try
                {
                    _bitmap = ImageUtils.ToBitmap(map.Data.Image);
                }
                catch
                {
                    _bitmap = null;
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                int size = Math.Max(1, Math.Min(512, Math.Min(ClientSize.Width - 20, ClientSize.Height - 20)));
                int left = (ClientSize.Width - size) / 2;
                int top = (ClientSize.Height - size) / 2;
                var bounds = new Rectangle(left, top, size, size);
                var graphics = e.Graphics;
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(Assets.ItemFrame, bounds);
                if (_bitmap != null)
                    graphics.DrawImage(_bitmap, bounds);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    _bitmap?.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
