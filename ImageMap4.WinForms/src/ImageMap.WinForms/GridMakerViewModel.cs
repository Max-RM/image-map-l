using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using SixLabors.ImageSharp.Processing;

namespace ImageMap4
{
    public class GridMakerViewModel : ObservableObject
    {
        public MainViewModel Parent { get; }
        public ObservableList<Selectable<Map>> MapSource { get; }
        public Map?[,] Grid { get; private set; } = new Map?[1, 1];
        private readonly List<Map?> _flatGrid = new();

        private int _gridWidth = 1;
        public int GridWidth
        {
            get => _gridWidth;
            set { _gridWidth = Math.Max(1, value); UpdateGrid(); OnPropertyChanged(); }
        }

        private int _gridHeight = 1;
        public int GridHeight
        {
            get => _gridHeight;
            set { _gridHeight = Math.Max(1, value); UpdateGrid(); OnPropertyChanged(); }
        }

        public GridMakerViewModel(MainViewModel parent, ObservableList<Selectable<Map>> source)
        {
            Parent = parent;
            MapSource = source;
            source.ItemChanged += MapSource_ItemChanged;
            var selected = source.Where(x => x.IsSelected).Select(x => x.Item).ToList();
            _flatGrid.AddRange(selected);
            int sqrt = (int)Math.Ceiling(Math.Sqrt(Math.Max(1, selected.Count)));
            for (int i = 0; i < sqrt; i++)
            {
                bool Check(int val)
                {
                    if (val <= 0)
                        return false;
                    int div = Math.DivRem(selected.Count, val, out int remain);
                    if (remain == 0 && selected.Count > 0)
                    {
                        GridWidth = val;
                        GridHeight = div;
                        return true;
                    }
                    return false;
                }
                if (Check(sqrt + i) || Check(sqrt - i))
                    break;
            }
            if (selected.Count == 0)
                UpdateGrid();
        }

        public void MoveMap(int fromX, int fromY, int toX, int toY)
        {
            if (fromX == toX && fromY == toY)
                return;
            int fromIndex = fromY * GridWidth + fromX;
            int toIndex = toY * GridWidth + toX;
            while (_flatGrid.Count <= toIndex)
                _flatGrid.Add(null);
            while (_flatGrid.Count <= fromIndex)
                _flatGrid.Add(null);
            _flatGrid[toIndex] = _flatGrid[fromIndex];
            _flatGrid[fromIndex] = null;
            UpdateGrid();
        }

        private void MapSource_ItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Selectable<Map>.IsSelected) || sender is not Selectable<Map> map)
                return;
            if (map.IsSelected && !_flatGrid.Contains(map.Item))
            {
                int index = _flatGrid.IndexOf(null);
                if (index == -1)
                    _flatGrid.Add(map.Item);
                else
                    _flatGrid[index] = map.Item;
            }
            else
                _flatGrid.Remove(map.Item);
            UpdateGrid();
        }

        private void UpdateGrid()
        {
            var newGrid = new Map?[GridWidth, GridHeight];
            for (int i = 0; i < Math.Min(newGrid.Length, _flatGrid.Count); i++)
            {
                int y = Math.DivRem(i, GridWidth, out int x);
                newGrid[x, y] = _flatGrid[i];
            }
            Grid = newGrid;
            OnPropertyChanged(nameof(Grid));
        }
    }

    public class ImageViewModel : ObservableObject
    {
        public GridMakerViewModel GridMaker { get; }
        public MainViewModel Parent => GridMaker.Parent;

        public ImageViewModel(GridMakerViewModel child)
        {
            GridMaker = child;
        }

        public SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> CreateImage()
        {
            var output = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(
                128 * GridMaker.GridWidth, 128 * GridMaker.GridHeight);
            output.Mutate(o =>
            {
                for (int y = 0; y < GridMaker.GridHeight; y++)
                {
                    for (int x = 0; x < GridMaker.GridWidth; x++)
                    {
                        var img = GridMaker.Grid[x, y];
                        if (img != null)
                            o.DrawImage(img.Data.Image, new SixLabors.ImageSharp.Point(x * 128, y * 128), 1f);
                    }
                }
            });
            return output;
        }
    }
}
