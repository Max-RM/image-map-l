using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ImageMap4;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SharpImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using SharpSize = SixLabors.ImageSharp.Size;

namespace ImageMap.WinForms
{
    public sealed class ImportForm : Form
    {
        public ImportViewModel ViewModel { get; } = new();

        private readonly ImportPreviewPanel _preview = new();
        private readonly NumericUpDown _width = new();
        private readonly NumericUpDown _height = new();
        private readonly ComboBox _scaling = new();
        private readonly ComboBox _fill = new();
        private readonly ComboBox _algorithm = new();
        private readonly ComboBox _dithering = new();
        private readonly Panel _javaOptions = new();
        private readonly Button _background = new();
        private readonly Button _confirmAll = new();
        private readonly Button _discardAll = new();
        private readonly FlowLayoutPanel _thumbs = new();
        private readonly Panel _thumbBar = new();
        private Bitmap? _currentBitmap;

        public ImportForm()
        {
            Text = "Import";
            Width = 500;
            Height = 640;
            MinimumSize = new Size(420, 520);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 12f);
            KeyPreview = true;
            BackColor = Color.White;

            ViewModel.PropertyChanged += ViewModelChanged;
            ViewModel.OnClosed += (_, _) => Close();
            ViewModel.ImageQueue.CollectionChanged += (_, _) => RebuildThumbs();

            BuildLayout();
            Shown += (_, _) => RefreshPreview();
            KeyDown += OnKeyDown;
            FormClosed += (_, _) =>
            {
                _currentBitmap?.Dispose();
                _currentBitmap = null;
            };
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _preview.Dock = DockStyle.Fill;
            _preview.BackColor = Color.LightGray;
            root.Controls.Add(_preview, 0, 0);

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0)
            };

            var transforms = CenteredRow();
            transforms.Controls.Add(IconButton("↺", () => { ViewModel.Rotate(-90); RefreshPreview(); }));
            transforms.Controls.Add(IconButton("↻", () => { ViewModel.Rotate(90); RefreshPreview(); }));
            transforms.Controls.Add(IconButton("↔", () => { ViewModel.FlipHorizontal(); RefreshPreview(); }));
            transforms.Controls.Add(IconButton("↕", () => { ViewModel.FlipVertical(); RefreshPreview(); }));
            _background.Size = new Size(40, 40);
            _background.Margin = new Padding(10, 0, 0, 0);
            _background.Click += (_, _) => { ViewModel.ChangeBackground(); RefreshPreview(); UpdateBackgroundButton(); };
            transforms.Controls.Add(_background);
            bottom.Controls.Add(transforms);

            var sizes = CenteredRow();
            sizes.Controls.Add(Labeled("Width:", _width));
            sizes.Controls.Add(Labeled("Height:", _height));
            _scaling.DropDownStyle = ComboBoxStyle.DropDownList;
            _scaling.DisplayMember = "Name";
            foreach (var option in ViewModel.ScaleOptions)
                _scaling.Items.Add(option);
            _scaling.SelectedItem = ViewModel.ScaleChoice;
            _scaling.SelectedIndexChanged += (_, _) =>
            {
                if (_scaling.SelectedItem is ImportViewModel.ScalingOption option)
                    ViewModel.ScaleChoice = option;
                RefreshPreview();
            };
            _scaling.Width = 100;
            sizes.Controls.Add(Labeled("Scaling:", _scaling, true));
            _fill.DropDownStyle = ComboBoxStyle.DropDownList;
            _fill.DisplayMember = "Name";
            foreach (var option in ViewModel.StretchOptions)
                _fill.Items.Add(option);
            _fill.SelectedItem = ViewModel.StretchChoice;
            _fill.SelectedIndexChanged += (_, _) =>
            {
                if (_fill.SelectedItem is ImportViewModel.StretchOption option)
                    ViewModel.StretchChoice = option;
                RefreshPreview();
            };
            _fill.Width = 100;
            sizes.Controls.Add(Labeled("Fill:", _fill, true));
            bottom.Controls.Add(sizes);

            _width.Minimum = 1;
            _width.Maximum = 100;
            _width.Value = 1;
            _width.Width = 60;
            _width.ValueChanged += (_, _) => { ViewModel.GridWidth = (int)_width.Value; RefreshPreview(); };
            _height.Minimum = 1;
            _height.Maximum = 100;
            _height.Value = 1;
            _height.Width = 60;
            _height.ValueChanged += (_, _) => { ViewModel.GridHeight = (int)_height.Value; RefreshPreview(); };

            _algorithm.DropDownStyle = ComboBoxStyle.DropDownList;
            _algorithm.DisplayMember = "Name";
            foreach (var option in ViewModel.AlgorithmOptions)
                _algorithm.Items.Add(option);
            _algorithm.SelectedItem = ViewModel.AlgorithmChoice;
            _algorithm.SelectedIndexChanged += (_, _) =>
            {
                if (_algorithm.SelectedItem is ImportViewModel.AlgorithmOption option)
                    ViewModel.AlgorithmChoice = option;
            };
            _algorithm.Width = 140;
            _dithering.DropDownStyle = ComboBoxStyle.DropDownList;
            _dithering.DisplayMember = "Name";
            foreach (var option in ViewModel.DitherOptions)
                _dithering.Items.Add(option);
            _dithering.SelectedItem = ViewModel.DitherChoice;
            _dithering.SelectedIndexChanged += (_, _) =>
            {
                if (_dithering.SelectedItem is ImportViewModel.DitherOption option)
                    ViewModel.DitherChoice = option;
            };
            _dithering.Width = 140;
            var javaRow = CenteredRow();
            javaRow.Controls.Add(Labeled("Algorithm:", _algorithm, true));
            javaRow.Controls.Add(Labeled("Dithering:", _dithering, true));
            _javaOptions.AutoSize = true;
            _javaOptions.Controls.Add(javaRow);
            bottom.Controls.Add(_javaOptions);

            var actions = CenteredRow();
            actions.Margin = new Padding(0, 5, 0, 10);
            actions.Controls.Add(TextButton("Confirm", ViewModel.Confirm));
            actions.Controls.Add(TextButton("Discard", ViewModel.Discard));
            _confirmAll.Text = "Confirm All";
            _confirmAll.AutoSize = true;
            _confirmAll.Padding = new Padding(5);
            _confirmAll.Margin = new Padding(10, 0, 0, 0);
            _confirmAll.Click += (_, _) => ViewModel.ConfirmAll();
            _discardAll.Text = "Discard All";
            _discardAll.AutoSize = true;
            _discardAll.Padding = new Padding(5);
            _discardAll.Margin = new Padding(10, 0, 0, 0);
            _discardAll.Click += (_, _) => ViewModel.DiscardAll();
            actions.Controls.Add(_confirmAll);
            actions.Controls.Add(_discardAll);
            bottom.Controls.Add(actions);

            _thumbs.AutoSize = true;
            _thumbs.WrapContents = false;
            _thumbs.FlowDirection = FlowDirection.LeftToRight;
            _thumbs.Padding = new Padding(6);
            _thumbBar.Height = 92;
            _thumbBar.Dock = DockStyle.Top;
            _thumbBar.AutoScroll = true;
            _thumbBar.BackColor = Color.DimGray;
            _thumbBar.Controls.Add(_thumbs);
            _thumbBar.Width = 480;
            bottom.Controls.Add(_thumbBar);

            root.Controls.Add(bottom, 0, 1);
            Controls.Add(root);
            UpdateBackgroundButton();
            UpdateVisibility();
        }

        private static FlowLayoutPanel CenteredRow()
        {
            return new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 4, 0, 4)
            };
        }

        private static Control Labeled(string text, Control control, bool center = false)
        {
            var panel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0, 3, 10, 0)
            };
            panel.Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                TextAlign = center ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            });
            panel.Controls.Add(control);
            return panel;
        }

        private static Button IconButton(string text, Action click)
        {
            var button = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Size = new Size(40, 40),
                Margin = new Padding(5, 0, 0, 0)
            };
            button.Click += (_, _) => click();
            return button;
        }

        private static Button TextButton(string text, Action click)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                Padding = new Padding(5),
                Margin = new Padding(10, 0, 0, 0)
            };
            button.Click += (_, _) => click();
            return button;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                ViewModel.Discard();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                ViewModel.DiscardAll();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter && e.Shift)
            {
                ViewModel.ConfirmAll();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                ViewModel.Confirm();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Left)
            {
                ViewModel.Navigate(-1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                ViewModel.Navigate(1);
                e.Handled = true;
            }
        }

        private void ViewModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (IsDisposed)
                return;
            if (e.PropertyName == nameof(ImportViewModel.CurrentImage) ||
                e.PropertyName == nameof(ImportViewModel.CurrentMode) ||
                e.PropertyName == nameof(ImportViewModel.BackgroundColorChoice) ||
                e.PropertyName == nameof(ImportViewModel.GridWidth) ||
                e.PropertyName == nameof(ImportViewModel.GridHeight))
            {
                RefreshPreview();
                UpdateBackgroundButton();
                UpdateTitle();
                RebuildThumbs();
            }
            if (e.PropertyName == nameof(ImportViewModel.JavaMode) ||
                e.PropertyName == nameof(ImportViewModel.HadMultiple))
            {
                UpdateVisibility();
            }
        }

        private void UpdateVisibility()
        {
            _javaOptions.Visible = ViewModel.JavaMode;
            _confirmAll.Visible = ViewModel.HadMultiple;
            _discardAll.Visible = ViewModel.HadMultiple;
            _thumbBar.Visible = ViewModel.HadMultiple;
        }

        private void UpdateTitle()
        {
            Text = ViewModel.CurrentImage == null
                ? "Import"
                : "Import – " + ViewModel.CurrentImage.Source.Name;
        }

        private void UpdateBackgroundButton()
        {
            var color = ViewModel.BackgroundColorChoice.Color;
            _background.BackColor = color.A == 0 ? Color.LightGray : color;
            using var canvas = new Bitmap(25, 25);
            using var graphics = Graphics.FromImage(canvas);
            graphics.Clear(Color.White);
            using (var brush = new SolidBrush(color.A == 0 ? Color.Transparent : color))
            using (var pen = new Pen(Color.Black))
            {
                if (color.A == 0)
                {
                    using var hatch = new HatchBrush(HatchStyle.DiagonalCross, Color.Silver, Color.White);
                    graphics.FillRectangle(hatch, 0, 0, 24, 24);
                }
                else
                    graphics.FillRectangle(brush, 0, 0, 24, 24);
                graphics.DrawRectangle(pen, 0, 0, 24, 24);
            }
            var old = _background.Image;
            _background.Image = (Bitmap)canvas.Clone();
            old?.Dispose();
        }

        private void RefreshPreview()
        {
            _currentBitmap?.Dispose();
            _currentBitmap = null;
            var image = ViewModel.CurrentImage;
            if (image != null)
            {
                try
                {
                    _currentBitmap = ImageUtils.ToBitmap(image.Source.Image.Value);
                }
                catch (Exception ex)
                {
                    CrashLog.Error("Import preview", ex);
                }
            }
            _preview.Set(ViewModel, _currentBitmap);
            if (image != null)
                image.PropertyChanged += (_, _) => _preview.Invalidate();
        }

        private void RebuildThumbs()
        {
            _thumbs.SuspendLayout();
            foreach (Control control in _thumbs.Controls)
                control.Dispose();
            _thumbs.Controls.Clear();
            foreach (var preview in ViewModel.ImageQueue)
            {
                var box = new ThumbnailBox(preview, preview == ViewModel.CurrentImage);
                box.Click += (_, _) => ViewModel.SwitchImage(preview);
                _thumbs.Controls.Add(box);
            }
            _thumbs.ResumeLayout();
        }

        private sealed class ThumbnailBox : PictureBox
        {
            public ThumbnailBox(PreviewImage preview, bool selected)
            {
                Size = new Size(72, 72);
                SizeMode = PictureBoxSizeMode.Zoom;
                Margin = new Padding(10, 10, 0, 10);
                BackColor = Color.LightGray;
                BorderStyle = BorderStyle.FixedSingle;
                Padding = new Padding(selected ? 4 : 0);
                try
                {
                    Image = ImageUtils.ToBitmap(preview.Source.Image.Value);
                }
                catch
                {
                }
                if (selected)
                    BackColor = Color.LightGreen;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    Image?.Dispose();
                base.Dispose(disposing);
            }
        }
    }

    internal sealed class ImportPreviewPanel : Control
    {
        private ImportViewModel? _viewModel;
        private Bitmap? _bitmap;
        private Bitmap? _mapPreview;
        private string? _mapPreviewKey;

        public ImportPreviewPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public void Set(ImportViewModel viewModel, Bitmap? bitmap)
        {
            _viewModel = viewModel;
            _bitmap = bitmap;
            InvalidateMapPreview();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                InvalidateMapPreview();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var graphics = e.Graphics;
            graphics.Clear(Color.LightGray);
            graphics.SmoothingMode = SmoothingMode.None;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.CompositingQuality = CompositingQuality.HighSpeed;
            if (_viewModel == null)
                return;
            int cols = Math.Max(1, _viewModel.GridWidth);
            int rows = Math.Max(1, _viewModel.GridHeight);
            int cell = Math.Max(1, Math.Min(ClientSize.Width / cols, ClientSize.Height / rows));
            int gridW = cell * cols;
            int gridH = cell * rows;
            int ox = (ClientSize.Width - gridW) / 2;
            int oy = (ClientSize.Height - gridH) / 2;
            var dest = new Rectangle(ox, oy, gridW, gridH);

            var bg = _viewModel.BackgroundColorChoice.Color;
            if (bg.A == 0)
            {
                using var hatch = new HatchBrush(HatchStyle.LargeCheckerBoard, Color.Gainsboro, Color.White);
                graphics.FillRectangle(hatch, dest);
            }
            else
            {
                using var brush = new SolidBrush(bg);
                graphics.FillRectangle(brush, dest);
            }

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                    graphics.DrawImage(Assets.ItemFrame, ox + x * cell, oy + y * cell, cell, cell);
            }

            var preview = EnsureMapPreview(dest.Size);
            if (preview != null)
            {
                graphics.DrawImageUnscaled(preview, dest.X, dest.Y);
                using var gridPen = new Pen(Color.FromArgb(80, Color.Black));
                for (int x = 1; x < cols; x++)
                    graphics.DrawLine(gridPen, ox + x * cell, oy, ox + x * cell, oy + gridH);
                for (int y = 1; y < rows; y++)
                    graphics.DrawLine(gridPen, ox, oy + y * cell, ox + gridW, oy + y * cell);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        private void InvalidateMapPreview()
        {
            _mapPreview?.Dispose();
            _mapPreview = null;
            _mapPreviewKey = null;
        }

        private Bitmap? EnsureMapPreview(Size dest)
        {
            if (_viewModel?.CurrentImage == null || dest.Width < 1 || dest.Height < 1)
            {
                InvalidateMapPreview();
                return null;
            }
            var image = _viewModel.CurrentImage;
            string key = string.Join("|",
                dest.Width, dest.Height,
                _viewModel.GridWidth, _viewModel.GridHeight,
                image.Rotation, image.ScaleX, image.ScaleY,
                _viewModel.StretchChoice.Name, _viewModel.ScaleChoice.Name,
                _viewModel.BackgroundColorChoice.Color.ToArgb(),
                image.Source.Name);
            if (_mapPreview != null && _mapPreviewKey == key)
                return _mapPreview;
            InvalidateMapPreview();
            try
            {
                _mapPreview = RenderMapPreview(dest);
                _mapPreviewKey = key;
            }
            catch (Exception ex)
            {
                CrashLog.Error("Import map preview", ex);
            }
            return _mapPreview;
        }

        // Compose at 128×128 per map cell (ImageSharp, not GDI+), then nearest-neighbour
        // upscale. ARM GDI+ ignores InterpolationMode.NearestNeighbor and blurs instead.
        private Bitmap? RenderMapPreview(Size dest)
        {
            if (_viewModel?.CurrentImage == null)
                return null;
            var preview = _viewModel.CurrentImage;
            SharpImage source = preview.Source.Image.Value;
            int cols = Math.Max(1, _viewModel.GridWidth);
            int rows = Math.Max(1, _viewModel.GridHeight);
            var mapSize = new SharpSize(128 * cols, 128 * rows);
            var sampler = _viewModel.ScaleChoice.Sampler(new SharpSize(source.Width, source.Height));
            using var canvas = source.Clone();
            canvas.Mutate(x =>
            {
                if (preview.Rotation != 0)
                    x.Rotate((float)preview.Rotation);
                if (preview.ScaleX < 0)
                    x.Flip(FlipMode.Horizontal);
                if (preview.ScaleY < 0)
                    x.Flip(FlipMode.Vertical);
                x.Resize(new ResizeOptions
                {
                    Size = mapSize,
                    Sampler = sampler,
                    Mode = _viewModel.StretchChoice.Mode
                });
                x.Resize(new ResizeOptions
                {
                    Size = mapSize,
                    Mode = ResizeMode.BoxPad,
                    Sampler = KnownResamplers.NearestNeighbor,
                    PadColor = _viewModel.BackgroundColorChoice.Pixel
                });
                x.BackgroundColor(_viewModel.BackgroundColorChoice.Pixel);
                if (dest.Width != mapSize.Width || dest.Height != mapSize.Height)
                    x.Resize(dest.Width, dest.Height, KnownResamplers.NearestNeighbor);
            });
            return ImageUtils.ToBitmap(canvas);
        }
    }
}
