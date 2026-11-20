using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ImageMap4;

namespace ImageMap.WinForms
{
    public sealed class ImageExportForm : Form
    {
        private readonly ImageViewModel _viewModel;
        private readonly NumericUpDown _width = new();
        private readonly NumericUpDown _height = new();
        private readonly GridPreviewPanel _preview;
        private bool _updating;

        public ImageExportForm(ImageViewModel viewModel)
        {
            _viewModel = viewModel;
            _preview = new GridPreviewPanel(viewModel.GridMaker);
            Text = "Create Image";
            Width = 940;
            Height = 450;
            MinimumSize = new Size(560, 360);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 12f);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var options = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            _width.Minimum = 1;
            _width.Maximum = 64;
            _width.Width = 60;
            _width.Value = viewModel.GridMaker.GridWidth;
            _width.ValueChanged += (_, _) =>
            {
                if (!_updating)
                    viewModel.GridMaker.GridWidth = (int)_width.Value;
                _preview.Invalidate();
            };
            _height.Minimum = 1;
            _height.Maximum = 64;
            _height.Width = 60;
            _height.Value = viewModel.GridMaker.GridHeight;
            _height.ValueChanged += (_, _) =>
            {
                if (!_updating)
                    viewModel.GridMaker.GridHeight = (int)_height.Value;
                _preview.Invalidate();
            };
            options.Controls.Add(new Label { Text = "Width:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
            options.Controls.Add(_width);
            options.Controls.Add(new Label { Text = "Height:", AutoSize = true, Margin = new Padding(12, 6, 6, 0) });
            options.Controls.Add(_height);
            root.Controls.Add(options, 0, 0);

            _preview.Dock = DockStyle.Fill;
            _preview.BackColor = Color.DimGray;
            root.Controls.Add(_preview, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 8, 0, 0)
            };
            var combined = new Button { Text = "Export Combined", AutoSize = true, Padding = new Padding(5) };
            var separate = new Button { Text = "Export Separately", AutoSize = true, Padding = new Padding(5), Margin = new Padding(10, 0, 0, 0) };
            var cancel = new Button { Text = "Cancel", AutoSize = true, Padding = new Padding(5), Margin = new Padding(10, 0, 0, 0), DialogResult = DialogResult.Cancel };
            combined.Click += (_, _) => ExportCombined();
            separate.Click += (_, _) => ExportSeparate();
            buttons.Controls.Add(combined);
            buttons.Controls.Add(separate);
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 2);
            Controls.Add(root);
            CancelButton = cancel;

            viewModel.GridMaker.PropertyChanged += (_, _) =>
            {
                _updating = true;
                _width.Value = viewModel.GridMaker.GridWidth;
                _height.Value = viewModel.GridMaker.GridHeight;
                _updating = false;
                _preview.Invalidate();
            };
        }

        private void ExportCombined()
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Export the full image",
                Filter = "Image Files|*.png;|All Files|*",
                FileName = "maps.png"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            using var image = _viewModel.CreateImage();
            ImageUtils.SavePng(image, dialog.FileName);
            Close();
        }

        private void ExportSeparate()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select the folder to save maps to"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            foreach (var item in _viewModel.GridMaker.MapSource.Where(x => x.IsSelected))
                ImageUtils.SavePng(item.Item.Data.Image, Path.Combine(dialog.SelectedPath, $"map_{item.Item.ID}.png"));
            Close();
        }

        private sealed class GridPreviewPanel : Control
        {
            private readonly GridMakerViewModel _grid;
            public GridPreviewPanel(GridMakerViewModel grid)
            {
                _grid = grid;
                DoubleBuffered = true;
                AllowDrop = true;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var graphics = e.Graphics;
                graphics.Clear(Color.DimGray);
                int cols = Math.Max(1, _grid.GridWidth);
                int rows = Math.Max(1, _grid.GridHeight);
                int cell = Math.Max(8, Math.Min((ClientSize.Width - 16) / cols, (ClientSize.Height - 16) / rows));
                int ox = (ClientSize.Width - cell * cols) / 2;
                int oy = (ClientSize.Height - cell * rows) / 2;
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                    {
                        var rect = new Rectangle(ox + x * cell, oy + y * cell, cell, cell);
                        graphics.DrawRectangle(Pens.Gray, rect);
                        var map = _grid.Grid[x, y];
                        if (map == null)
                            continue;
                        try
                        {
                            using var bitmap = ImageUtils.ToBitmap(map.Data.Image);
                            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                            graphics.DrawImage(bitmap, rect);
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }
    }
}
