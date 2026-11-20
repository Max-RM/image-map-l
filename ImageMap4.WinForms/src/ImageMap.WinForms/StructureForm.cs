using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ImageMap4;

namespace ImageMap.WinForms
{
    public sealed class StructureForm : Form
    {
        private const int TileWidth = 72;
        private const int TileHeight = 90;

        private readonly World _world;
        private readonly IReadOnlyList<Map> _maps;
        private readonly Dictionary<long, Bitmap> _bitmaps = new Dictionary<long, Bitmap>();
        private readonly NumericUpDown _width = new NumericUpDown();
        private readonly NumericUpDown _height = new NumericUpDown();
        private readonly CheckBox _glowing = new CheckBox();
        private readonly CheckBox _invisible = new CheckBox();
        private readonly ComboBox _inventory = new ComboBox();
        private readonly Label _inventoryInfo = new Label();
        private readonly Panel _scroll = new Panel();
        private readonly TableLayoutPanel _grid = new TableLayoutPanel();
        private readonly Label _hint = new Label();
        private Map?[] _tiles = Array.Empty<Map?>();
        private int _selectedIndex = -1;
        private int _gridWidth = 1;
        private int _gridHeight = 1;
        private bool _updating;

        public StructureGrid? Structure { get; private set; }
        public IInventory? Inventory { get; private set; }

        public StructureForm(World world, IReadOnlyList<Map> maps)
        {
            _world = world;
            _maps = maps;

            Text = "Generate Structure";
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(840, 640);
            MinimumSize = new Size(560, 420);
            KeyPreview = true;

            InitializeOptions();
            BuildLayout();
            LoadInventories();

            var (width, height) = ChooseSize(maps.Count);
            _tiles = new Map?[width * height];
            for (int i = 0; i < maps.Count && i < _tiles.Length; i++)
                _tiles[i] = maps[i];
            _updating = true;
            _width.Value = width;
            _height.Value = height;
            _updating = false;
            ResizeTiles(width, height);

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete && _selectedIndex >= 0)
                {
                    ClearSelected();
                    e.Handled = true;
                }
            };
        }

        private void InitializeOptions()
        {
            _width.Minimum = 1;
            _width.Maximum = 64;
            _width.Value = 1;
            _width.Width = 60;
            _width.ValueChanged += (s, e) =>
            {
                if (!_updating)
                    ApplySize();
            };

            _height.Minimum = 1;
            _height.Maximum = 64;
            _height.Value = 1;
            _height.Width = 60;
            _height.ValueChanged += (s, e) =>
            {
                if (!_updating)
                    ApplySize();
            };

            _glowing.AutoSize = true;
            _glowing.Text = "Glowing item frames";
            _glowing.Visible = _world.SupportsGlowFrames;

            _invisible.AutoSize = true;
            _invisible.Text = "Invisible item frames";
            _invisible.Visible = _world.SupportsInvisibleFrames;

            _inventory.DropDownStyle = ComboBoxStyle.DropDownList;
            _inventory.Width = 260;
            _inventory.SelectedIndexChanged += (s, e) => UpdateInventoryInfo();

            _inventoryInfo.AutoSize = true;
            _inventoryInfo.ForeColor = SystemColors.GrayText;

            _hint.AutoSize = false;
            _hint.Dock = DockStyle.Fill;
            _hint.Height = 36;
            _hint.TextAlign = ContentAlignment.MiddleLeft;
            _hint.ForeColor = SystemColors.GrayText;
            _hint.Text = "Drag a tile onto another tile to swap them. Select a tile and press Delete to leave a gap.";
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var options = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Padding = new Padding(0, 0, 0, 6)
            };
            options.Controls.Add(new Label { Text = "Width:", AutoSize = true, Anchor = AnchorStyles.Left });
            options.Controls.Add(_width);
            options.Controls.Add(new Label { Text = "Height:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 3, 3, 3) });
            options.Controls.Add(_height);
            options.Controls.Add(new Label { Text = "Inventory:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 3, 3, 3) });
            options.Controls.Add(_inventory);
            options.Controls.Add(_glowing);
            options.Controls.Add(_invisible);
            options.Controls.Add(_inventoryInfo);

            _grid.AutoSize = true;
            _grid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _grid.ColumnCount = 1;
            _grid.RowCount = 1;
            _grid.Location = new Point(0, 0);
            _grid.Padding = new Padding(2);

            _scroll.Dock = DockStyle.Fill;
            _scroll.AutoScroll = true;
            _scroll.BorderStyle = BorderStyle.FixedSingle;
            _scroll.Controls.Add(_grid);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 0)
            };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
            var ok = new Button { Text = "Create", Width = 90 };
            ok.Click += (s, e) => Confirm();
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            root.Controls.Add(options, 0, 0);
            root.Controls.Add(_scroll, 0, 1);
            root.Controls.Add(_hint, 0, 2);
            root.Controls.Add(buttons, 0, 3);
            Controls.Add(root);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void LoadInventories()
        {
            var inventories = new List<IInventory> { new NoInventory() };
            inventories.AddRange(_world.GetInventories());
            _inventory.DataSource = inventories;
            _inventory.DisplayMember = nameof(IInventory.Name);
            _inventory.SelectedIndex = inventories.Count > 1 ? 1 : 0;
            UpdateInventoryInfo();
        }

        private void UpdateInventoryInfo()
        {
            if (_inventory.SelectedItem is not IInventory inventory)
            {
                _inventoryInfo.Text = "No inventory selected.";
                return;
            }
            _inventoryInfo.Text = inventory is NoInventory
                ? "Structure data will be written without adding an item."
                : $"{inventory.FreeSlots} free slot(s).";
        }

        private void ApplySize()
        {
            int width = (int)_width.Value;
            int height = (int)_height.Value;
            if (width * height < _maps.Count)
            {
                _updating = true;
                _width.Value = _gridWidth;
                _height.Value = _gridHeight;
                _updating = false;
                MessageBox.Show(this,
                    $"The grid must have at least one cell for each of the {_maps.Count} selected map(s).",
                    "Generate Structure", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ResizeTiles(width, height);
        }

        private void ResizeTiles(int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            _gridWidth = width;
            _gridHeight = height;
            var next = new Map?[width * height];
            Array.Copy(_tiles, next, Math.Min(_tiles.Length, next.Length));
            _tiles = next;
            _selectedIndex = -1;
            RebuildGrid(width, height);
        }

        private void RebuildGrid(int width, int height)
        {
            _grid.SuspendLayout();
            foreach (Control control in _grid.Controls.Cast<Control>().ToArray())
            {
                _grid.Controls.Remove(control);
                control.Dispose();
            }
            _grid.Controls.Clear();
            _grid.ColumnStyles.Clear();
            _grid.RowStyles.Clear();
            _grid.ColumnCount = width;
            _grid.RowCount = height;

            for (int x = 0; x < width; x++)
                _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, TileWidth + 8));
            for (int y = 0; y < height; y++)
                _grid.RowStyles.Add(new RowStyle(SizeType.Absolute, TileHeight + 8));

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    var tile = new TileControl(index, _tiles[index], GetBitmap)
                    {
                        Size = new Size(TileWidth, TileHeight),
                        Margin = new Padding(2)
                    };
                    tile.SelectRequested += SelectTile;
                    tile.SwapRequested += SwapTiles;
                    _grid.Controls.Add(tile, x, y);
                }
            }
            _grid.ResumeLayout();
            UpdateTileSelection();
        }

        private Bitmap GetBitmap(Map map)
        {
            if (!_bitmaps.TryGetValue(map.ID, out var bitmap))
            {
                bitmap = ImageUtils.ToBitmap(map.Data.Image);
                _bitmaps[map.ID] = bitmap;
            }
            return bitmap;
        }

        private void SelectTile(int index)
        {
            _selectedIndex = index;
            UpdateTileSelection();
        }

        private void UpdateTileSelection()
        {
            foreach (var tile in _grid.Controls.OfType<TileControl>())
            {
                tile.IsSelected = tile.Index == _selectedIndex;
                tile.Invalidate();
            }
        }

        private void SwapTiles(int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= _tiles.Length || to >= _tiles.Length)
                return;
            (_tiles[from], _tiles[to]) = (_tiles[to], _tiles[from]);
            foreach (var tile in _grid.Controls.OfType<TileControl>())
                tile.Map = _tiles[tile.Index];
            _selectedIndex = to;
            UpdateTileSelection();
        }

        private void ClearSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _tiles.Length)
                return;
            _tiles[_selectedIndex] = null;
            foreach (var tile in _grid.Controls.OfType<TileControl>())
                tile.Map = _tiles[tile.Index];
            UpdateTileSelection();
        }

        private void Confirm()
        {
            int width = (int)_width.Value;
            int height = (int)_height.Value;
            int capacity = width * height;
            if (capacity < _maps.Count)
            {
                MessageBox.Show(this,
                    $"The grid has {capacity} cell(s) but {_maps.Count} map(s) were selected. Increase the width or height.",
                    "Generate Structure", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var grid = new Map?[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    grid[x, y] = _tiles[y * width + x];
            }

            Structure = new StructureGrid(grid)
            {
                GlowingFrames = _glowing.Checked,
                InvisibleFrames = _invisible.Checked
            };
            Inventory = _inventory.SelectedItem as IInventory ?? new NoInventory();
            DialogResult = DialogResult.OK;
            Close();
        }

        private static (int Width, int Height) ChooseSize(int count)
        {
            if (count <= 0)
                return (1, 1);
            int width = (int)Math.Ceiling(Math.Sqrt(count));
            int height = (int)Math.Ceiling(count / (double)width);
            return (width, height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var bitmap in _bitmaps.Values)
                    bitmap.Dispose();
                _bitmaps.Clear();
            }
            base.Dispose(disposing);
        }

        private sealed class TileControl : Control
        {
            private readonly Func<Map, Bitmap> _getBitmap;
            private Point _dragStart;
            private bool _dragging;

            public int Index { get; }
            public Map? Map { get; set; }
            public bool IsSelected { get; set; }
            public event Action<int>? SelectRequested;
            public event Action<int, int>? SwapRequested;

            public TileControl(int index, Map? map, Func<Map, Bitmap> getBitmap)
            {
                Index = index;
                Map = map;
                _getBitmap = getBitmap;
                AllowDrop = true;
                DoubleBuffered = true;
                Font = new Font("Segoe UI", 8f);
                SetStyle(ControlStyles.Selectable, false);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var graphics = e.Graphics;
                graphics.Clear(SystemColors.ControlDarkDark);
                var imageRect = new Rectangle(4, 4, Width - 8, Height - 24);
                if (Map != null)
                {
                    var bitmap = _getBitmap(Map);
                    float scale = Math.Min(imageRect.Width / (float)bitmap.Width, imageRect.Height / (float)bitmap.Height);
                    int width = Math.Max(1, (int)(bitmap.Width * scale));
                    int height = Math.Max(1, (int)(bitmap.Height * scale));
                    int x = imageRect.X + (imageRect.Width - width) / 2;
                    int y = imageRect.Y + (imageRect.Height - height) / 2;
                    graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                    graphics.PixelOffsetMode = PixelOffsetMode.Half;
                    graphics.DrawImage(bitmap, new Rectangle(x, y, width, height));
                }
                else
                {
                    TextRenderer.DrawText(graphics, "empty", Font, imageRect, SystemColors.GrayText,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                using var pen = new Pen(IsSelected ? Color.DodgerBlue : SystemColors.ControlDark, IsSelected ? 2f : 1f);
                graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                string text = Map?.ID.ToString() ?? "-";
                TextRenderer.DrawText(graphics, text, Font, new Rectangle(0, Height - 19, Width, 18), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button == MouseButtons.Left)
                {
                    _dragStart = e.Location;
                    SelectRequested?.Invoke(Index);
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (e.Button == MouseButtons.Left && !_dragging && Map != null &&
                    (Math.Abs(e.X - _dragStart.X) > 4 || Math.Abs(e.Y - _dragStart.Y) > 4))
                {
                    _dragging = true;
                    DoDragDrop(this, DragDropEffects.Move);
                    _dragging = false;
                }
            }

            protected override void OnDragEnter(DragEventArgs e)
            {
                base.OnDragEnter(e);
                e.Effect = e.Data.GetDataPresent(typeof(TileControl)) ? DragDropEffects.Move : DragDropEffects.None;
            }

            protected override void OnDragOver(DragEventArgs e)
            {
                base.OnDragOver(e);
                e.Effect = e.Data.GetDataPresent(typeof(TileControl)) ? DragDropEffects.Move : DragDropEffects.None;
            }

            protected override void OnDragDrop(DragEventArgs e)
            {
                base.OnDragDrop(e);
                if (e.Data.GetData(typeof(TileControl)) is TileControl source)
                    SwapRequested?.Invoke(source.Index, Index);
            }
        }
    }
}
