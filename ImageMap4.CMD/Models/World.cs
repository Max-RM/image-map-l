using fNbt;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace ImageMap4;

public abstract class World
{
    public string Folder { get; }
    public string FolderName => Path.GetFileName(Folder);
    public abstract string Name { get; }
    public abstract string WorldIcon { get; }
    public abstract DateTime AccessDate { get; }
    public World(string folder)
    {
        Folder = folder;
    }

    public abstract bool SupportsInvisibleFrames { get; }
    public abstract bool SupportsGlowFrames { get; }
    public abstract bool SupportsChests { get; }
    public abstract bool SupportsStructures { get; }

    public abstract bool IsIdTaken(long id);
    public abstract IAsyncEnumerable<Map> GetMapsAsync();
    public abstract void AddMaps(IEnumerable<Map> maps);
    public abstract void RemoveMaps(IEnumerable<long> ids);
    public abstract void AddStructures(IEnumerable<StructureGrid> structures, IInventory inventory);
    public abstract void AddChest(IEnumerable<long> ids, IInventory inventory);
    public abstract IEnumerable<IInventory> GetInventories();

    // A single map item with no slot assigned, ready to be packed into an inventory.
    public abstract NbtCompound MakeMapLeaf(long id);
    // Writes the structure data (files/LDB) and returns the corresponding inventory items.
    public abstract List<NbtCompound> WriteStructureItems(IEnumerable<StructureGrid> structures);
    // Arranges leaf items into the top-level items that go into inventory slots.
    public abstract List<NbtCompound> PackInventoryItems(List<NbtCompound> leaves, InventoryMode mode);
    // How many inventory slots a given number of leaf items would occupy in the given mode.
    public abstract int SlotsNeeded(int itemCount, InventoryMode mode);

    public List<NbtCompound> BuildInventoryItems(IEnumerable<long> mapIds, IEnumerable<StructureGrid> structures, InventoryMode mode)
    {
        var leaves = mapIds.Select(MakeMapLeaf).ToList();
        leaves.AddRange(WriteStructureItems(structures));
        return PackInventoryItems(leaves, mode);
    }

    public void AddMapsToInventory(IEnumerable<long> ids, IInventory inventory, InventoryMode mode)
    {
        inventory.AddItems(PackInventoryItems(ids.Select(MakeMapLeaf).ToList(), mode));
    }
    protected abstract void ProcessImage(Image<Rgba32> image, ProcessSettings settings);
    protected abstract byte[] EncodeColors(Image<Rgba32> image);
    public MapData[,] MakeMaps(ImportSettings settings)
    {
        using var image = settings.Preview.Source.Image.Value;
        image.Mutate(x =>
        {
            x.Rotate((float)settings.Preview.Rotation);
            if (settings.Preview.ScaleX == -1)
                x.Flip(FlipMode.Horizontal);
            if (settings.Preview.ScaleY == -1)
                x.Flip(FlipMode.Vertical);
            x.Resize(new ResizeOptions()
            {
                Size = new(128 * settings.Width, 128 * settings.Height),
                Sampler = settings.Sampler.Value,
                Mode = settings.ResizeMode
            });
            // uniform scale crops to content, so we need to re-add transparency to get correct size
            x.Resize(new ResizeOptions()
            {
                Size = new(128 * settings.Width, 128 * settings.Height),
                Mode = ResizeMode.BoxPad
            });
            x.BackgroundColor(settings.BackgroundColor);
        });
        var original = Split(image, settings.Width, settings.Height);
        ProcessImage(image, settings.ProcessSettings);
        var finished = Split(image, settings.Width, settings.Height);
        var result = new MapData[settings.Width, settings.Height];
        for (int y = 0; y < settings.Height; y++)
        {
            for (int x = 0; x < settings.Width; x++)
            {
                result[x, y] = new MapData(finished[x, y], original[x, y], EncodeColors(finished[x, y]));
            }
        }
        return result;
    }

    private static Image<Rgba32>[,] Split(Image<Rgba32> source, int columns, int rows)
    {
        var result = new Image<Rgba32>[columns, rows];
        int width = source.Width / columns;
        int height = source.Height / rows;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                var tile = new Image<Rgba32>(width, height);
                source.ProcessPixelRows(tile, (sa, ta) =>
                {
                    for (int i = 0; i < height; i++)
                    {
                        var source = sa.GetRowSpan(height * y + i);
                        var target = ta.GetRowSpan(i);
                        source.Slice(width * x, width).CopyTo(target);
                    }
                });
                result[x, y] = tile;
            }
        }
        return result;
    }
}

public record ImportSettings(PreviewImage Preview, int Width, int Height, Lazy<IResampler> Sampler, ResizeMode ResizeMode, Rgba32 BackgroundColor, ProcessSettings ProcessSettings);
public record ProcessSettings(IDither? Dither, IColorAlgorithm Algorithm);
