using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ImageMap4;

namespace ImageMap.WinForms
{
    public sealed class MapCard : Control
    {
        private const int ImageSize = 128;
        private const int ImageTop = 4;
        public const int CardWidth = 136;
        public const int CardHeight = 158;
        public const int CardMargin = 3;
        public const int CellWidth = CardWidth + CardMargin * 2;
        public const int CellHeight = CardHeight + CardMargin * 2;

        private readonly Font _labelFont = new Font("Segoe UI", 9.75f, FontStyle.Bold, GraphicsUnit.Point);
        private Bitmap? _normal;
        private Bitmap? _original;
        private bool _hover;
        private Selectable<Map>? _selectable;

        public Selectable<Map> Selectable => _selectable!;
        public Map Map => Selectable.Item;
        public bool IsImporting { get; }
        public bool IsConflict { get; set; }

        public bool IsSelected
        {
            get => _selectable != null && _selectable.IsSelected;
            set
            {
                if (_selectable != null)
                    _selectable.IsSelected = value;
            }
        }

        public event MouseEventHandler? CardMouseDown;

        public MapCard(bool importing)
        {
            IsImporting = importing;
            Size = new Size(CardWidth, CardHeight);
            Margin = new Padding(CardMargin);
            TabStop = false;
            AllowDrop = true;
            BackColor = Color.FromArgb(255, 0xDD, 0xDD, 0xDD);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public void Bind(Selectable<Map> selectable, bool conflict)
        {
            if (ReferenceEquals(_selectable, selectable))
            {
                if (IsConflict != conflict)
                {
                    IsConflict = conflict;
                    Invalidate();
                }
                return;
            }
            Unbind();
            _selectable = selectable;
            IsConflict = conflict;
            _hover = false;
            LoadBitmaps();
            selectable.PropertyChanged += SelectableChanged;
            Invalidate();
        }

        public void Unbind()
        {
            if (_selectable != null)
                _selectable.PropertyChanged -= SelectableChanged;
            _selectable = null;
            DisposeBitmaps();
        }

        private void LoadBitmaps()
        {
            DisposeBitmaps();
            if (_selectable == null)
                return;
            try
            {
                _normal = ImageUtils.ToBitmap(Map.Data.Image);
                _original = ReferenceEquals(Map.Data.Image, Map.Data.Original)
                    ? _normal
                    : ImageUtils.ToBitmap(Map.Data.Original);
            }
            catch
            {
                _normal = null;
                _original = null;
            }
        }

        private void DisposeBitmaps()
        {
            if (_original != null && !ReferenceEquals(_normal, _original))
                _original.Dispose();
            _normal?.Dispose();
            _normal = null;
            _original = null;
        }

        private void SelectableChanged(object? sender, PropertyChangedEventArgs e) => Invalidate();

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_selectable == null)
                return;
            var graphics = e.Graphics;
            using (var background = new SolidBrush(IsSelected
                       ? Color.FromArgb(255, 0x68, 0xB3, 0xFF)
                       : Color.FromArgb(255, 0xDD, 0xDD, 0xDD)))
            {
                graphics.FillRectangle(background, ClientRectangle);
            }

            var image = _hover && _original != null ? _original : _normal;
            int left = (Width - ImageSize) / 2;
            var imageRect = new Rectangle(left, ImageTop, ImageSize, ImageSize);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(Assets.ItemFrame, imageRect);
            if (image != null)
                graphics.DrawImage(image, imageRect);

            var labelBounds = new Rectangle(left, ImageTop + ImageSize + 2, ImageSize - 20, 20);
            TextRenderer.DrawText(graphics, "map_" + Map.ID, _labelFont, labelBounds, Color.Black,
                TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis);
            if (IsConflict)
            {
                graphics.DrawImage(Assets.Warning,
                    new Rectangle(Width - 20, ImageTop + ImageSize + 5, 12, 11));
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            CardMouseDown?.Invoke(this, e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (Parent is MapListPanel list)
            {
                list.ScrollByDelta(e.Delta);
                return;
            }
            base.OnMouseWheel(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Unbind();
                _labelFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public sealed class MapListPanel : Panel
    {
        private readonly List<Selectable<Map>> _items = new();
        private readonly Dictionary<Selectable<Map>, MapCard> _visible = new();
        private readonly Stack<MapCard> _pool = new();
        private Selectable<Map>? _lastClicked;
        private ObservableList<Selectable<Map>>? _source;
        private bool _showEmpty = true;
        private ICollection<long> _conflicts = Array.Empty<long>();
        private bool _updating;
        private bool _visibleQueued;

        public string EmptyText { get; set; } = "";
        public bool IsImporting { get; }
        public event EventHandler<MapCard>? ContextRequested;
        public event EventHandler? SelectionChanged;
        public event EventHandler? DeleteRequested;
        public event Action<string[]>? FilesDropped;

        public IReadOnlyList<Selectable<Map>> Selectables => _items;
        public IReadOnlyList<Map> Maps => _items.Select(x => x.Item).ToList();
        public IReadOnlyList<Map> SelectedMaps =>
            _items.Where(x => x.IsSelected).Select(x => x.Item).ToList();
        public IReadOnlyList<Selectable<Map>> SelectedSelectables =>
            _items.Where(x => x.IsSelected).ToList();
        public bool AnySelected => _items.Any(x => x.IsSelected);
        public Map? FirstSelected => _items.FirstOrDefault(x => x.IsSelected)?.Item;

        public MapListPanel(bool importing)
        {
            IsImporting = importing;
            AutoScroll = true;
            BackColor = Color.White;
            AllowDrop = importing;
            TabStop = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            HorizontalScroll.Enabled = false;
            HorizontalScroll.Visible = false;
            DragEnter += OnFilesDragEnter;
            DragDrop += OnFilesDragDrop;
        }

        public void Bind(ObservableList<Selectable<Map>> source)
        {
            if (_source != null)
                _source.CollectionChanged -= SourceChanged;
            _source = source;
            source.CollectionChanged += SourceChanged;
            Rebuild();
        }

        public void SetShowEmpty(bool showEmpty)
        {
            if (_showEmpty == showEmpty)
                return;
            _showEmpty = showEmpty;
            Rebuild();
        }

        public void ScrollByDelta(int delta)
        {
            int y = -AutoScrollPosition.Y - Math.Sign(delta) * SystemInformation.MouseWheelScrollLines * 16;
            AutoScrollPosition = new Point(0, Math.Max(0, y));
            UpdateVisible();
        }

        private void SourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                BeginInvoke(new NotifyCollectionChangedEventHandler(SourceChanged), sender, e);
                return;
            }
            if (e.Action == NotifyCollectionChangedAction.Reset ||
                e.Action == NotifyCollectionChangedAction.Replace ||
                e.Action == NotifyCollectionChangedAction.Move)
            {
                Rebuild();
                return;
            }
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (Selectable<Map> item in e.NewItems)
                    AddItem(item);
                QueueVisibleUpdate();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                return;
            }
            if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (Selectable<Map> item in e.OldItems)
                    RemoveItem(item);
                QueueVisibleUpdate();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Rebuild()
        {
            RecycleAll();
            _items.Clear();
            _lastClicked = null;
            ResetScroll();
            if (_source != null)
            {
                foreach (var item in _source)
                    AddItem(item);
            }
            UpdateVisible();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void FlushLayout() => UpdateVisible();

        private bool PassesFilter(Selectable<Map> item) =>
            _showEmpty || IsImporting || !item.Item.Data.IsEmpty;

        private void AddItem(Selectable<Map> item)
        {
            if (!PassesFilter(item))
                return;
            int index = _items.FindIndex(x => x.Item.ID > item.Item.ID);
            if (index < 0)
                _items.Add(item);
            else
                _items.Insert(index, item);
        }

        private void RemoveItem(Selectable<Map> item)
        {
            _items.Remove(item);
            if (_visible.TryGetValue(item, out var card))
            {
                _visible.Remove(item);
                Recycle(card);
            }
            if (ReferenceEquals(_lastClicked, item))
                _lastClicked = null;
        }

        public void SetConflicts(ICollection<long> conflicts)
        {
            _conflicts = conflicts;
            foreach (var pair in _visible)
            {
                bool conflict = conflicts.Contains(pair.Key.Item.ID);
                if (pair.Value.IsConflict != conflict)
                {
                    pair.Value.IsConflict = conflict;
                    pair.Value.Invalidate();
                }
            }
        }

        public void SelectAll()
        {
            foreach (var item in _items)
                item.IsSelected = true;
            InvalidateVisible();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void DeselectAll()
        {
            foreach (var item in _items)
                item.IsSelected = false;
            InvalidateVisible();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectOnly(MapCard card)
        {
            var chosen = card.Selectable;
            foreach (var item in _items)
                item.IsSelected = ReferenceEquals(item, chosen);
            InvalidateVisible();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void InvalidateVisible()
        {
            foreach (var card in _visible.Values)
                card.Invalidate();
        }

        private void CardMouseDown(object? sender, MouseEventArgs e)
        {
            if (sender is not MapCard card)
                return;
            Focus();
            if (e.Button == MouseButtons.Right)
            {
                if (!card.IsSelected)
                    SelectOnly(card);
                ContextRequested?.Invoke(this, card);
                return;
            }
            if (e.Button != MouseButtons.Left)
                return;

            bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;
            if (shift && _lastClicked != null)
            {
                int first = _items.IndexOf(_lastClicked);
                int last = _items.IndexOf(card.Selectable);
                bool selected = !card.IsSelected;
                if (first >= 0 && last >= 0)
                {
                    for (int i = Math.Min(first, last); i <= Math.Max(first, last); i++)
                        _items[i].IsSelected = selected;
                    InvalidateVisible();
                }
            }
            else
            {
                card.IsSelected = !card.IsSelected;
                card.Invalidate();
            }
            _lastClicked = card.Selectable;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                SelectAll();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.D)
            {
                DeselectAll();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                DeleteRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnContextMenuStripChanged(EventArgs e)
        {
            base.OnContextMenuStripChanged(e);
            foreach (var card in _visible.Values)
                card.ContextMenuStrip = ContextMenuStrip;
        }

        protected override Point ScrollToControl(Control activeControl) => DisplayRectangle.Location;

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            UpdateVisible();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            UpdateVisible();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            UpdateVisible();
            base.OnLayout(levent);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateVisible();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_items.Count == 0 && !string.IsNullOrEmpty(EmptyText))
            {
                var bounds = new Rectangle(12, 12, Math.Max(1, ClientSize.Width - 24), Math.Max(1, ClientSize.Height - 24));
                TextRenderer.DrawText(e.Graphics, EmptyText, Font, bounds, Color.Gray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            }
        }

        private void QueueVisibleUpdate()
        {
            if (_visibleQueued)
                return;
            if (!IsHandleCreated)
            {
                UpdateVisible();
                return;
            }
            _visibleQueued = true;
            BeginInvoke(new Action(() =>
            {
                _visibleQueued = false;
                UpdateVisible();
            }));
        }

        private void ResetScroll()
        {
            AutoScrollMinSize = Size.Empty;
            AutoScrollPosition = Point.Empty;
        }

        private int ViewportWidth
        {
            get
            {
                int width = Width > 50 ? Width : Parent?.ClientSize.Width ?? Width;
                return Math.Max(MapCard.CellWidth, width - SystemInformation.VerticalScrollBarWidth);
            }
        }

        private int ColumnCount => Math.Max(1, ViewportWidth / MapCard.CellWidth);

        private void UpdateVisible()
        {
            if (_updating)
                return;
            _updating = true;
            try
            {
                HorizontalScroll.Visible = false;
                HorizontalScroll.Enabled = false;

                int columns = ColumnCount;
                int rows = _items.Count == 0 ? 0 : (_items.Count + columns - 1) / columns;
                var minSize = new Size(0, rows * MapCard.CellHeight);
                if (AutoScrollMinSize != minSize)
                    AutoScrollMinSize = minSize;

                int viewHeight = Math.Max(1, ClientSize.Height);
                int scrollY = Math.Max(0, -AutoScrollPosition.Y);
                int firstRow = Math.Max(0, scrollY / MapCard.CellHeight - 1);
                int lastRow = Math.Min(rows - 1, (scrollY + viewHeight) / MapCard.CellHeight + 1);
                int first = firstRow * columns;
                int last = rows == 0 ? -1 : Math.Min(_items.Count - 1, (lastRow + 1) * columns - 1);

                var keep = new HashSet<Selectable<Map>>();
                if (last >= first && first >= 0)
                {
                    for (int i = first; i <= last; i++)
                        keep.Add(_items[i]);
                }

                var remove = new List<Selectable<Map>>();
                foreach (var key in _visible.Keys)
                {
                    if (!keep.Contains(key))
                        remove.Add(key);
                }
                foreach (var key in remove)
                {
                    Recycle(_visible[key]);
                    _visible.Remove(key);
                }

                int scrollX = AutoScrollPosition.X;
                int posY = AutoScrollPosition.Y;
                for (int i = first; i <= last && i >= 0; i++)
                {
                    var item = _items[i];
                    if (!_visible.TryGetValue(item, out var card))
                    {
                        card = TakeCard();
                        card.Bind(item, _conflicts.Contains(item.Item.ID));
                        card.ContextMenuStrip = ContextMenuStrip;
                        _visible[item] = card;
                        Controls.Add(card);
                    }
                    int col = i % columns;
                    int row = i / columns;
                    card.Location = new Point(
                        col * MapCard.CellWidth + MapCard.CardMargin + scrollX,
                        row * MapCard.CellHeight + MapCard.CardMargin + posY);
                }

                int maxScroll = Math.Max(0, minSize.Height - ClientSize.Height);
                if (-AutoScrollPosition.Y > maxScroll)
                    AutoScrollPosition = new Point(0, maxScroll);
            }
            finally
            {
                _updating = false;
            }
            Invalidate();
        }

        private MapCard TakeCard()
        {
            if (_pool.Count > 0)
                return _pool.Pop();
            var card = new MapCard(IsImporting);
            card.CardMouseDown += CardMouseDown;
            if (IsImporting)
            {
                card.DragEnter += OnFilesDragEnter;
                card.DragDrop += OnFilesDragDrop;
            }
            return card;
        }

        private void Recycle(MapCard card)
        {
            card.Unbind();
            Controls.Remove(card);
            _pool.Push(card);
        }

        private void RecycleAll()
        {
            foreach (var card in _visible.Values)
                Recycle(card);
            _visible.Clear();
        }

        private static bool IsFileDrop(DragEventArgs e) =>
            e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop);

        private void OnFilesDragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = IsFileDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnFilesDragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                FilesDropped?.Invoke(files);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_source != null)
                    _source.CollectionChanged -= SourceChanged;
                RecycleAll();
                while (_pool.Count > 0)
                    _pool.Pop().Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
