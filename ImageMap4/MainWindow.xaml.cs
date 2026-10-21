using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using GongSolutions.Wpf.DragDrop;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using fNbt;
using System.Net.Http;
using System.Text.Json;

namespace ImageMap4;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, IDropTarget
{
    private MainViewModel ViewModel => (MainViewModel)this.DataContext;
    public ICommand PasteCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenWorldFolderCommand { get; }
    public ICommand OpenMapFileCommand { get; }
    public ICommand ChangeIDCommand { get; }
    public ICommand ExportImageCommand { get; }
    public ICommand AddToInventoryCommand { get; }
    public ICommand PreviewCommand { get; }
    public MainWindow()
    {
        PasteCommand = new RelayCommand(() =>
        {
            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                OpenImages(files.Cast<string>().Select(PendingSource.FromPath));
            }
            else if (Clipboard.ContainsImage())
            {
                var source = Clipboard.GetImage();
                using var stream = new MemoryStream();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.Save(stream);
                stream.Position = 0;
                OpenImages(new[]
                {
                    new PendingSource(() => SixLabors.ImageSharp.Image.Load<Rgba32>(stream), "Pasted image")
                });
            }
        });
        OpenWorldFolderCommand = new RelayCommand<World>(x =>
        {
            Process.Start("explorer.exe", $"\"{x.Folder}\"");
        });
        OpenMapFileCommand = new RelayCommand<Map>(x =>
        {
            if (ViewModel.SelectedWorld is JavaWorld world)
            {
                string path = world.Version.MapFileLocation(world.Folder, x.ID);
                Process.Start("explorer.exe", $"/select, \"{path.Replace('/', '\\')}\"");
            }
        });
        ChangeIDCommand = new RelayCommand<IList<Selectable<Map>>>(x =>
        {
            var selected = x.Where(x => x.IsSelected);
            var window = new ChangeIDWindow(selected.FirstOrDefault()?.Item.ID ?? 0, selected.Count(), x.Except(selected).Select(x => x.Item.ID).ToHashSet());
            window.Owner = this;
            if (window.ShowDialog() ?? false)
            {
                if (window.Result == ChangeResult.Confirmed)
                    ViewModel.ChangeIDs(x, selected, window.ID);
                else if (window.Result == ChangeResult.Auto)
                    ViewModel.AutoIDs(x, selected);
            }
        });
        PreviewCommand = new RelayCommand<Selectable<Map>>(x =>
        {
            if (x == null)
                return;
            var window = new PreviewWindow(x.Item) { Owner = this };
            window.ShowDialog();
        });
        AddToInventoryCommand = new RelayCommand<IList<Selectable<Map>>>(x =>
        {
            var selected = x.Where(x => x.IsSelected);
            var window = new AddToInventoryWindow(this.ViewModel);
            window.Owner = this;
            if (window.ShowDialog() ?? false)
            {
                var inventory = window.SelectedInventory;
                if (inventory is NoInventory)
                {
                    MessageBox.Show(this, "No inventory was selected.", "Image Map",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (inventory != null && ViewModel.SelectedWorld != null)
                {
                    try
                    {
                        var world = ViewModel.SelectedWorld;
                        var free = inventory.FreeSlots;
                        var leaves = selected.Select(x => world.MakeMapLeaf(x.Item.ID)).ToList();
                        var packed = leaves.Count > free
                            ? world.PackInventoryItems(leaves, InventoryMode.Chests)
                            : leaves;
                        inventory.AddItems(packed);
                        int added = Math.Min(packed.Count, free);
                        int after = inventory.FreeSlots;
                        ViewModel.TransferStatus = free <= 0
                            ? $"No free slots in {inventory.Name}."
                            : after >= free
                                ? $"Could not write to {inventory.Name}; close the world in Minecraft and try again."
                                : added < packed.Count
                                    ? $"Added {added} of {packed.Count} map item(s) to {inventory.Name}; not enough free slots."
                                    : $"Added {added} map item(s) to {inventory.Name}.";
                    }
                    catch (Exception ex)
                    {
                        ViewModel.TransferStatus = $"Could not write: {ex.Message}";
                    }
                }
            }
        });
        ExportImageCommand = new RelayCommand<ObservableList<Selectable<Map>>>(x =>
        {
            var selected = x.Where(x => x.IsSelected).ToList();
            if (selected.Count == 1)
            {
                var dialog = new SaveFileDialog();
                dialog.Title = "Export the image";
                dialog.Filter = "Image Files|*.png;|All Files|*";
                dialog.FileName = $"map_{selected[0].Item.ID}.png";
                if (dialog.ShowDialog() == true)
                    selected[0].Item.Data.Image.Save(dialog.FileName);
            }
            else
            {
                if (ImageWindow == null || !ImageWindow.IsVisible)
                {
                    ImageWindow = new(new ImageViewModel(new GridMakerViewModel(ViewModel, x)));
                    ImageWindow.Owner = this;
                }
                ImageWindow.Show();
                ImageWindow.Activate();
            }
        });
        DeleteCommand = new RelayCommand(() =>
        {
            if (ImportList.IsFocused)
                ViewModel.DiscardCommand.Execute(ViewModel.ImportingMaps);
            else if (ExistingList.IsFocused)
                ViewModel.DeleteCommand.Execute(ViewModel.ExistingMaps);
        });
        InitializeComponent();
        ViewModel.ConfirmInsufficientSlots = ConfirmInsufficientSlots;
    }

    private InsufficientSlotsAction ConfirmInsufficientSlots(int needed, int free)
    {
        var result = MessageBox.Show(this,
            $"Not enough free inventory slots.\n\nNeeded: {needed}\nFree: {free}\n\n" +
            "Yes — ignore and continue: the map data will still be saved, but the inventory will not be changed.\n" +
            "No — pause: nothing is changed and your imported maps stay in the list.",
            "Image Map", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes ? InsufficientSlotsAction.Ignore : InsufficientSlotsAction.Pause;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            if (TryOpenWorldFolder(args[1]))
                TabList.SelectedItem = MapsTab;
            if (args.Length > 2)
                OpenImages(args.Skip(2).Select(PendingSource.FromPath));
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        Properties.Settings.Default.Save();
    }

    private void JavaFolder_Click(object sender, RoutedEventArgs e)
    {
        Properties.Settings.Default.JavaFolders ??= new();
        PickWorldFolders(Properties.Settings.Default.JavaFolders, WorldSavePathRegistry.GetJavaPaths,
            "Java worlds", "Check the folder(s) where your Java worlds are saved.");
    }

    private void BedrockFolder_Click(object sender, RoutedEventArgs e)
    {
        Properties.Settings.Default.BedrockFolders ??= new();
        PickWorldFolders(Properties.Settings.Default.BedrockFolders, WorldSavePathRegistry.GetBedrockPaths,
            "Bedrock worlds", "Check the folder(s) where your Bedrock worlds are saved. All folders found on this computer are listed.");
    }

    private void PickWorldFolders(
        System.Collections.Specialized.StringCollection? folders,
        Func<IEnumerable<string>, List<WorldSavePath>> discover,
        string title,
        string description)
    {
        folders ??= new();
        bool adding = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
        var configured = folders.Cast<string>().ToList();
        var candidates = discover(configured);
        foreach (var candidate in candidates)
        {
            candidate.IsChecked = adding || configured.Any(x =>
                string.Equals(Environment.ExpandEnvironmentVariables(x).TrimEnd('\\', '/'), candidate.Path, StringComparison.OrdinalIgnoreCase));
        }
        var window = new WorldFoldersWindow(title, description, candidates, !adding) { Owner = this };
        if (window.ShowDialog() != true)
            return;
        if (window.ReplaceExisting)
            folders.Clear();
        foreach (var path in window.SelectedPaths)
        {
            if (!folders.Cast<string>().Any(x => string.Equals(Environment.ExpandEnvironmentVariables(x).TrimEnd('\\', '/'), path, StringComparison.OrdinalIgnoreCase)))
                folders.Add(path);
        }
        Properties.Settings.Default.Save();
        ((MainViewModel)this.DataContext).RefreshWorlds();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog();
        dialog.Multiselect = true;
        dialog.Title = "Select images to import";
        dialog.Filter = "Image Files|*.png; *.bmp; *.jpg; *.jpeg; *.gif|All Files|*";
        if (dialog.ShowDialog() == true)
            OpenImages(dialog.FileNames.Select(PendingSource.FromPath));
    }

    private ImageWindow? ImageWindow;
    private ImportWindow? ImportWindow;
    private void OpenImages(IEnumerable<PendingSource> images)
    {
        if (ImportWindow == null || !ImportWindow.IsVisible)
        {
            ImportWindow = new();
            ImportWindow.Owner = this;
            ImportWindow.ViewModel.JavaMode = ViewModel.SelectedWorld is JavaWorld;
            ImportWindow.ViewModel.OnConfirmed += (s, e) => _ = ViewModel.AddImports(e);
        }
        ImportWindow.Show();
        ImportWindow.Activate();
        ImportWindow.ViewModel.AddImages(images);
    }

    private StructureWindow? StructureWindow;
    private void GenerateStructureButton_Click(object sender, RoutedEventArgs e)
    {
        if (StructureWindow == null || !StructureWindow.IsVisible)
        {
            StructureWindow = new(new StructureViewModel(new GridMakerViewModel(this.ViewModel, this.ViewModel.ExistingMaps)));
            StructureWindow.Owner = this;
            StructureWindow.ViewModel.OnConfirmed += (s, e) => Try(() => ViewModel.SelectedWorld.AddStructures(new[] { e.grid }, e.inventory));
        }
        StructureWindow.Show();
        StructureWindow.Activate();
    }

    private void Try(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void IDropTarget.DragOver(IDropInfo dropInfo)
    {
        GetDropAction(dropInfo);
    }

    void IDropTarget.Drop(IDropInfo dropInfo)
    {
        GetDropAction(dropInfo)();
    }

    private Action GetDropAction(IDropInfo info)
    {
        if (info.Data is IDataObject data && data.GetDataPresent(DataFormats.FileDrop))
        {
            if (info.VisualTarget == ImportList)
            {
                info.Effects = DragDropEffects.Copy;
                return () =>
                {
                    var files = (IEnumerable<string>)data.GetData(DataFormats.FileDrop);
                    var list = new List<string>();
                    foreach (var file in files)
                    {
                        if (File.Exists(file))
                            list.Add(file);
                        else if (Directory.Exists(file))
                            list.AddRange(Directory.GetFiles(file));
                    }
                    OpenImages(list.Select(PendingSource.FromPath));
                };
            }
            else if (info.VisualTarget == WorldsTab)
            {
                info.Effects = DragDropEffects.Copy;
                return () =>
                {
                    var file = ((IEnumerable<string>)data.GetData(DataFormats.FileDrop)).First();
                    if (TryOpenWorldFolder(file))
                        TabList.SelectedItem = MapsTab;
                };
            }
        }
        return () => { };
    }

    private bool TryOpenWorldFolder(string folder)
    {
        if (Directory.Exists(folder) && File.Exists(Path.Combine(folder, "level.dat")))
        {
            if (Directory.Exists(Path.Combine(folder, "db")))
                TryOpenWorld(new BedrockWorld(folder));
            else
                TryOpenWorld(new JavaWorld(folder));
            return true;
        }
        return false;
    }

    private bool TryOpenWorld(World world)
    {
        if (world is JavaWorld java && java.Version == null)
        {
            ViewModel.SelectedWorld = null;
            return false;
        }
        if (world is BedrockWorld bedrock && bedrock.Version == null)
        {
            ViewModel.SelectedWorld = null;
            return false;
        }
        try
        {
            ViewModel.SelectedWorld = world;
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString());
        }
        ViewModel.SelectedWorld = null;
        return false;
    }

    private void JavaWorldList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (JavaWorldList.SelectedIndex != -1)
        {
            BedrockWorldList.SelectedIndex = -1;
            var success = TryOpenWorld((World)JavaWorldList.SelectedItem);
            if (!success)
            {
                JavaWorldList.SelectedIndex = -1;
            }
        }
    }

    private void BedrockWorldList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BedrockWorldList.SelectedIndex != -1)
        {
            JavaWorldList.SelectedIndex = -1;
            var success = TryOpenWorld((World)BedrockWorldList.SelectedItem);
            if (!success)
            {
                JavaWorldList.SelectedIndex = -1;
            }
        }
    }

    private void World_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.SelectedWorld != null)
        {
            TabList.SelectedItem = MapsTab;
        }
    }

    private void CloseMapsTab_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedWorld = null;
        JavaWorldList.SelectedIndex = -1;
        BedrockWorldList.SelectedIndex = -1;
        TabList.SelectedItem = WorldsTab;
    }
}

public class ConflictChecker : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is long id && values[1] is ICollection<long> maps)
            return maps.Contains(id);
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class ImageSharpConverter : OneWayConverter<Image<Rgba32>, ImageSource>
{
    private static readonly ConditionalWeakTable<Image<Rgba32>, ImageSource> Cache = new();

    public override ImageSource Convert(Image<Rgba32> value)
    {
        return Cache.GetValue(value, x =>
        {
            var source = new ImageSharpImageSource<Rgba32>(x);
            source.Freeze();
            return source;
        });
    }
}

public class DisplayJavaInventory : IInventory, INotifyPropertyChanged
{
    private readonly IInventory Wrapped;
    private static readonly HttpClient Client = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Name { get; private set; }
    public int FreeSlots => Wrapped.FreeSlots;
    public DisplayJavaInventory(IInventory wrapped)
    {
        Wrapped = wrapped;
        Name = wrapped.Name;
        if (Name.Length == 36)
        {
            // convert UUIDs to playernames
            // first check in cache, which is like a slow dictionary where keys are in even places and values in odd
            bool found = false;
            if (Properties.Settings.Default.UsernameCache == null)
                Properties.Settings.Default.UsernameCache = new();
            for (int i = 0; i < Properties.Settings.Default.UsernameCache.Count; i += 2)
            {
                if (Properties.Settings.Default.UsernameCache[i] == Name)
                {
                    Name = Properties.Settings.Default.UsernameCache[i + 1];
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                // get name from Mojang's API
                // if it fails, just ignore
                // if it succeeds, cache it forever
                Debug.WriteLine($"Looking up UUID {Name}");
                var result = Client.GetAsync($"https://api.mojang.com/user/profiles/{Name}/names");
                result.ContinueWith(x =>
                {
                    if (x.IsCompletedSuccessfully)
                    {
                        var response = x.Result.Content.ReadAsStringAsync().Result;
                        var json = JsonDocument.Parse(response);
                        if (json.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            string newname = json.RootElement[json.RootElement.GetArrayLength() - 1].GetProperty("name").GetString();
                            lock (Properties.Settings.Default.UsernameCache)
                            {
                                Properties.Settings.Default.UsernameCache.Add(Name);
                                Properties.Settings.Default.UsernameCache.Add(newname);
                            }
                            this.Name = newname;
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                        }
                    }
                });
            }
        }
    }

    public void AddItems(IEnumerable<NbtCompound> items)
    {
        Wrapped.AddItems(items);
    }
}
