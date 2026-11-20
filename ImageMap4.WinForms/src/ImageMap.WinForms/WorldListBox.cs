using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ImageMap4;

namespace ImageMap.WinForms
{
    public sealed class WorldItem : IDisposable
    {
        private Image? _icon;
        private bool _iconLoaded;

        public World World { get; }
        public string Folder => World.Folder;

        public WorldItem(World world)
        {
            World = world;
        }

        public string DisplayName => World.Name;
        public string FolderName => World.FolderName;
        public string VersionName => World.VersionLabel;

        public Image? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _iconLoaded = true;
                    try
                    {
                        if (File.Exists(World.WorldIcon))
                        {
                            using var loaded = Image.FromFile(World.WorldIcon);
                            _icon = new Bitmap(loaded);
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
                return _icon;
            }
        }

        public void Dispose()
        {
            _icon?.Dispose();
            _icon = null;
        }

        public override string ToString() => Path.GetFileName(World.Folder);
    }

    public sealed class WorldListBox : ListBox
    {
        private static readonly Color CardColor = Color.FromArgb(255, 0xE5, 0xE5, 0xE5);
        private static readonly Color SelectedColor = Color.FromArgb(255, 0xCC, 0xDD, 0xFF);
        private static readonly Color FolderColor = Color.FromArgb(255, 0x70, 0x70, 0x70);
        private static readonly Color VersionColor = Color.FromArgb(255, 0x60, 0x60, 0x60);
        private readonly Font _nameFont = new Font("Segoe UI", 12.75f, FontStyle.Regular, GraphicsUnit.Point);
        private readonly Font _smallFont = new Font("Segoe UI", 9.75f, FontStyle.Regular, GraphicsUnit.Point);

        public string EmptyText { get; set; } = "";

        public WorldListBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 64;
            IntegralHeight = false;
            BorderStyle = BorderStyle.None;
            BackColor = Color.White;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count)
                return;
            var item = (WorldItem)Items[e.Index];
            var graphics = e.Graphics;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            var card = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, e.Bounds.Width - 4, e.Bounds.Height - 4);
            using (var path = Assets.RoundedRectangle(card, 4))
            using (var background = new SolidBrush(selected ? SelectedColor : CardColor))
                graphics.FillPath(background, path);

            var icon = item.Icon;
            if (icon != null)
            {
                var iconRect = new Rectangle(card.X + 2, card.Y + 2, 60, 60);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                graphics.DrawImage(icon, iconRect);
            }

            int textLeft = card.X + 68;
            int textWidth = Math.Max(10, card.Right - textLeft - 6);
            int top = card.Y + 4;
            TextRenderer.DrawText(graphics, item.DisplayName, _nameFont,
                new Rectangle(textLeft, top, textWidth, 24), Color.Black,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(graphics, item.FolderName, _smallFont,
                new Rectangle(textLeft, top + 23, textWidth, 18), FolderColor,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(graphics, item.VersionName, _smallFont,
                new Rectangle(textLeft, top + 39, textWidth, 18), VersionColor,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Items.Count == 0 && !string.IsNullOrEmpty(EmptyText))
            {
                var bounds = new Rectangle(12, 12, Math.Max(1, ClientSize.Width - 24), Math.Max(1, ClientSize.Height - 24));
                TextRenderer.DrawText(e.Graphics, EmptyText, Font, bounds, Color.Gray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _nameFont.Dispose();
                _smallFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
