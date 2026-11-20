using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using fNbt;
using ImageMap.WinForms;
using TryashtarUtils.Utility;

namespace ImageMap4
{
    public class MainViewModel : ObservableObject
    {
        public ObservableCollection<JavaWorld> JavaWorlds { get; } = new();
        public ObservableCollection<BedrockWorld> BedrockWorlds { get; } = new();
        public ObservableList<Selectable<Map>> ImportingMaps { get; } = new();
        public ObservableList<Selectable<Map>> ExistingMaps { get; } = new();
        public List<StructureGrid> ImportingStructures { get; } = new();
        public ReadOnlyCollection<IInventory>? PlayerList { get; private set; }
        public HashSet<long> ConflictingIDs { get; } = new();
        public Action<Action> UiInvoke { get; set; } = action => action();
        public Func<int, int, InsufficientSlotsAction>? ConfirmInsufficientSlots { get; set; }

        private readonly UndoHistory _undoHistory = new();
        private FileStream? _sessionLock;
        private CancellationTokenSource? _mapCts;
        private Task? _refreshTask;
        private World? _selectedWorld;
        private string? _transferStatus;

        private static readonly IComparer<Selectable<Map>> Sorter =
            Comparer<Selectable<Map>>.Create((a, b) => a.Item.ID.CompareTo(b.Item.ID));

        public World? SelectedWorld
        {
            get => _selectedWorld;
            set
            {
                if (SameWorld(_selectedWorld, value))
                    return;
                ReleaseWorld();
                _selectedWorld = value;
                _undoHistory.Clear();
                OnPropertyChanged();
                OnPropertyChanged(nameof(StructuresAvailable));
                OnPropertyChanged(nameof(CreateStructuresEffective));
                if (_selectedWorld == null)
                    return;

                if (_selectedWorld is JavaWorld java)
                {
                    try
                    {
                        _sessionLock = new FileStream(Path.Combine(java.Folder, "session.lock"), FileMode.OpenOrCreate);
                        _sessionLock.Lock(0, 1);
                    }
                    catch (Exception ex)
                    {
                        CrashLog.Info("Could not lock session.lock: " + ex.Message);
                        CloseSessionLock();
                    }
                }

                _mapCts = new CancellationTokenSource();
                var token = _mapCts.Token;
                var world = _selectedWorld;
                _refreshTask = Task.Run(() => LoadWorldAsync(world, token));
            }
        }

        public static bool SameWorld(World? a, World? b)
        {
            if (a == null || b == null)
                return false;
            return string.Equals(a.Folder, b.Folder, StringComparison.OrdinalIgnoreCase);
        }

        public bool ShowEmptyMaps
        {
            get => AppSettings.Default.ShowEmptyMaps;
            set { AppSettings.Default.ShowEmptyMaps = value; OnPropertyChanged(); }
        }

        public bool CreateStructures
        {
            get => AppSettings.Default.CreateStructures;
            set { AppSettings.Default.CreateStructures = value; OnPropertyChanged(); OnPropertyChanged(nameof(CreateStructuresEffective)); }
        }

        public bool StructuresAvailable => SelectedWorld?.SupportsStructures == true;

        public bool CreateStructuresEffective
        {
            get => StructuresAvailable && AppSettings.Default.CreateStructures;
            set
            {
                if (!StructuresAvailable)
                    return;
                AppSettings.Default.CreateStructures = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CreateStructures));
            }
        }

        public bool CreateMaps
        {
            get => AppSettings.Default.CreateMaps;
            set { AppSettings.Default.CreateMaps = value; OnPropertyChanged(); }
        }

        public string? TransferStatus
        {
            get => _transferStatus;
            set { _transferStatus = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            ImportingMaps.ItemChanged += Maps_ItemChanged;
            ExistingMaps.ItemChanged += Maps_ItemChanged;
            ImportingMaps.CollectionChanged += Maps_CollectionChanged;
            ExistingMaps.CollectionChanged += Maps_CollectionChanged;
            RefreshWorlds();
        }

        public void Undo() => _undoHistory.Undo();
        public void Redo() => _undoHistory.Redo();

        public void DiscardSelected(IList<Selectable<Map>> source)
        {
            var maps = source.Where(x => x.IsSelected).ToList();
            if (maps.Count == 0)
                return;
            _undoHistory.Perform(
                () => RemoveRange(maps, source),
                () => InsertRange(maps, source, Sorter));
        }

        public void DeleteSelected(IList<Selectable<Map>> source)
        {
            var maps = source.Where(x => x.IsSelected).ToList();
            if (maps.Count == 0)
                return;
            _undoHistory.Perform(
                () =>
                {
                    RemoveRange(maps, source);
                    SelectedWorld?.RemoveMaps(maps.Select(x => x.Item.ID));
                },
                () =>
                {
                    InsertRange(maps, source, Sorter);
                    SelectedWorld?.AddMaps(maps.Select(x => x.Item));
                });
        }

        public void TransferAll()
        {
            if (SelectedWorld == null)
                return;
            try
            {
                TransferAllCore();
            }
            catch (Exception ex)
            {
                CrashLog.Error("TransferAll", ex);
                TransferStatus = "Could not write: " + ex.Message;
            }
        }

        private void TransferAllCore()
        {
            var overwritten = ExistingMaps.Where(x => ConflictingIDs.Contains(x.Item.ID)).ToList();
            var importing = ImportingMaps.ToList();
            var structures = CreateStructuresEffective ? ImportingStructures.ToList() : new List<StructureGrid>();
            var inventory = GetTargetInventory();
            int mapCount = CreateMaps ? importing.Count : 0;
            int total = mapCount + structures.Count;
            int free = inventory?.FreeSlots ?? 0;

            bool skipInventory = false;
            if (inventory != null && total > free)
            {
                int needed = SelectedWorld!.SlotsNeeded(total, InventoryMode.Chests);
                if (needed > free)
                {
                    var action = ConfirmInsufficientSlots?.Invoke(needed, free) ?? InsufficientSlotsAction.Pause;
                    if (action == InsufficientSlotsAction.Pause)
                        return;
                    skipInventory = true;
                }
            }

            var leaves = new List<NbtCompound>();
            if (CreateMaps && inventory != null)
                leaves.AddRange(importing.Select(x => SelectedWorld!.MakeMapLeaf(x.Item.ID)));
            if (structures.Count > 0)
                leaves.AddRange(SelectedWorld!.WriteStructureItems(structures));

            if (inventory != null && !skipInventory && leaves.Count > 0)
            {
                var packed = leaves.Count > free
                    ? SelectedWorld!.PackInventoryItems(leaves, InventoryMode.Chests)
                    : leaves;
                inventory.AddItems(packed);
                int added = Math.Min(packed.Count, free);
                int after = inventory.FreeSlots;
                TransferStatus = free <= 0
                    ? $"No free slots in {inventory.Name}; maps and structures were saved."
                    : after >= free
                        ? $"Could not write to {inventory.Name}; close the world in Minecraft and try again. Maps and structures were saved."
                        : added < packed.Count
                            ? $"Added {added} of {packed.Count} item(s) to {inventory.Name}; not enough free slots. Maps and structures were saved."
                            : $"Added {added} item(s) to {inventory.Name}.";
            }
            else if (skipInventory)
                TransferStatus = "Maps and structures were saved; inventory was skipped.";
            else if (inventory == null)
                TransferStatus = "Maps and structures were saved; no inventory selected.";
            else
                TransferStatus = "Nothing to add to the inventory.";

            ImportingStructures.Clear();
            _undoHistory.Perform(
                () =>
                {
                    SelectedWorld!.AddMaps(importing.Select(x => x.Item));
                    InsertRange(importing, ExistingMaps, Sorter);
                    RemoveRange(overwritten, ExistingMaps);
                    foreach (var map in ExistingMaps)
                        map.IsSelected = importing.Contains(map);
                    ImportingMaps.Clear();
                },
                () =>
                {
                    SelectedWorld!.RemoveMaps(importing.Select(x => x.Item.ID));
                    SelectedWorld.AddMaps(overwritten.Select(x => x.Item));
                    RemoveRange(importing, ExistingMaps);
                    InsertRange(overwritten, ExistingMaps, Sorter);
                    InsertRange(importing, ImportingMaps, Sorter);
                });
        }

        private void Maps_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateConflictingIDs();

        private void Maps_ItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Map.ID))
                UpdateConflictingIDs();
        }

        private void UpdateConflictingIDs()
        {
            ConflictingIDs.Clear();
            foreach (var id in ExistingMaps.Select(x => x.Item.ID).Intersect(ImportingMaps.Select(x => x.Item.ID)))
                ConflictingIDs.Add(id);
            OnPropertyChanged(nameof(ConflictingIDs));
        }

        private async Task LoadWorldAsync(World world, CancellationToken ct)
        {
            try
            {
                IEnumerable<IInventory> inventories = world.GetInventories();
                if (world is JavaWorld)
                    inventories = inventories.Select(x => new DisplayJavaInventory(x));
                var players = inventories.ToList();
                players.Insert(0, new NoInventory());
                UiInvoke(() =>
                {
                    if (ct.IsCancellationRequested)
                        return;
                    PlayerList = players.AsReadOnly();
                    OnPropertyChanged(nameof(PlayerList));
                });
            }
            catch (Exception ex)
            {
                CrashLog.Error("Load inventories", ex);
            }

            try
            {
                int added = 0;
                await foreach (var item in world.GetMapsAsync().WithCancellation(ct))
                {
                    ct.ThrowIfCancellationRequested();
                    var selectable = new Selectable<Map>(item);
                    UiInvoke(() =>
                    {
                        if (!ct.IsCancellationRequested)
                            Insert(selectable, ExistingMaps, Sorter);
                    });
                    added++;
                    if (added % 8 == 0)
                        await Task.Delay(1, ct);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                CrashLog.Error("RefreshMaps", ex);
                UiInvoke(() => TransferStatus = "Could not load maps: " + ex.Message);
            }
        }

        public void ReleaseWorld()
        {
            _mapCts?.Cancel();
            try { _refreshTask?.Wait(2000); } catch { }
            _mapCts?.Dispose();
            _mapCts = null;
            _refreshTask = null;
            CloseSessionLock();
            try { _selectedWorld?.Close(); } catch (Exception ex) { CrashLog.Info("World.Close: " + ex.Message); }
            DisposeLoadedMaps();
            ImportingMaps.Clear();
            ExistingMaps.Clear();
            ImportingStructures.Clear();
            ConflictingIDs.Clear();
            PlayerList = null;
            TransferStatus = null;
            OnPropertyChanged(nameof(PlayerList));
            OnPropertyChanged(nameof(ConflictingIDs));
            OnPropertyChanged(nameof(TransferStatus));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private void DisposeLoadedMaps()
        {
            foreach (var map in ImportingMaps.Concat(ExistingMaps))
            {
                try { map.Item.DisposeData(); } catch { }
            }
        }

        public void ChangeIDs(IList<Selectable<Map>> source, IEnumerable<Selectable<Map>> maps, long newId)
        {
            long id = newId;
            var changing = maps.Select(x => (from: x.Item.ID, to: id++, map: x.Item)).ToList();
            var newIds = changing.Select(x => x.to).ToHashSet();
            var replaced = source.Except(maps).Where(x => newIds.Contains(x.Item.ID)).ToList();
            var affected = maps.ToList();
            _undoHistory.Perform(
                () =>
                {
                    foreach (var (_, to, map) in changing)
                        map.ID = to;
                    RemoveRange(replaced, source);
                    RemoveRange(affected, source);
                    InsertRange(affected, source, Sorter);
                    if (source == ExistingMaps && SelectedWorld != null)
                    {
                        SelectedWorld.RemoveMaps(changing.Select(x => x.from));
                        SelectedWorld.AddMaps(changing.Select(x => x.map));
                    }
                },
                () =>
                {
                    foreach (var (from, _, map) in changing)
                        map.ID = from;
                    InsertRange(replaced, source, Sorter);
                    RemoveRange(affected, source);
                    InsertRange(affected, source, Sorter);
                    if (source == ExistingMaps && SelectedWorld != null)
                    {
                        SelectedWorld.RemoveMaps(newIds);
                        SelectedWorld.AddMaps(changing.Select(x => x.map));
                        SelectedWorld.AddMaps(replaced.Select(x => x.Item));
                    }
                });
        }

        public void AutoIDs(IList<Selectable<Map>> source, IEnumerable<Selectable<Map>> maps)
        {
            ChangeIDs(source, maps, NextFreeID());
        }

        private void Insert<T>(T item, IList<T> list, IComparer<T> sorter)
        {
            int index = list.BinarySearch(item, sorter);
            if (index < 0)
                index = ~index;
            list.Insert(index, item);
        }

        private void InsertRange<T>(IEnumerable<T> items, IList<T> list, IComparer<T> sorter)
        {
            foreach (var item in items)
                Insert(item, list, sorter);
        }

        private static void RemoveRange<T>(IEnumerable<T> items, IList<T> list) where T : class
        {
            foreach (var item in items)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(list[i], item))
                    {
                        list.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        public void RefreshWorlds()
        {
            JavaWorlds.Clear();
            BedrockWorlds.Clear();
            AppSettings.Default.JavaFolders ??= new StringCollection();
            if (AppSettings.Default.JavaFolders.Count == 0)
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "saves");
                AppSettings.Default.JavaFolders.Add(path);
            }
            foreach (var rawDir in AppSettings.Default.JavaFolders)
            {
                string javaDir = Environment.ExpandEnvironmentVariables(rawDir);
                if (!Directory.Exists(javaDir))
                    continue;
                var worlds = new List<JavaWorld>();
                foreach (var dir in Directory.GetDirectories(javaDir))
                {
                    if (!File.Exists(Path.Combine(dir, "level.dat")))
                        continue;
                    try
                    {
                        worlds.Add(new JavaWorld(dir));
                    }
                    catch (Exception ex)
                    {
                        CrashLog.Error("Error loading Java world " + dir, ex);
                    }
                }
                foreach (var world in worlds.OrderByDescending(x => x.AccessDate))
                    JavaWorlds.Add(world);
            }

            AppSettings.Default.BedrockFolders ??= new StringCollection();
            if (AppSettings.Default.BedrockFolders.Count == 0)
            {
                var fallback = WorldSavePathRegistry.GetDefaultBedrockPaths().FirstOrDefault();
                if (fallback != null)
                    AppSettings.Default.BedrockFolders.Add(fallback.Path);
            }
            foreach (var rawDir in AppSettings.Default.BedrockFolders)
            {
                string bedrockDir = Environment.ExpandEnvironmentVariables(rawDir);
                if (!Directory.Exists(bedrockDir))
                    continue;
                var worlds = new List<BedrockWorld>();
                foreach (var dir in Directory.GetDirectories(bedrockDir))
                {
                    if (!File.Exists(Path.Combine(dir, "level.dat")) || !Directory.Exists(Path.Combine(dir, "db")))
                        continue;
                    try
                    {
                        worlds.Add(new BedrockWorld(dir));
                    }
                    catch (Exception ex)
                    {
                        CrashLog.Error("Error loading Bedrock world " + dir, ex);
                    }
                }
                foreach (var world in worlds.OrderByDescending(x => x.AccessDate))
                    BedrockWorlds.Add(world);
            }
        }

        private IInventory? GetTargetInventory()
        {
            if (PlayerList == null || PlayerList.Count <= 1)
                return null;
            int index = AppSettings.Default.InventoryChoice;
            if (index <= 0 || index >= PlayerList.Count)
                index = 1;
            var inventory = PlayerList[index];
            return inventory is NoInventory ? null : inventory;
        }

        private long NextFreeID()
        {
            return ImportingMaps.Concat(ExistingMaps).Select(x => x.Item.ID).Concat(new[] { -1L }).Max() + 1;
        }

        public async Task AddImports(IList<ImportSettings> settings)
        {
            if (SelectedWorld == null)
                return;
            TransferStatus = null;
            long currentId = NextFreeID();
            var settingsList = new List<(ImportSettings settings, long firstId)>();
            foreach (var item in settings)
            {
                settingsList.Add((item, currentId));
                currentId += item.Width * item.Height;
            }
            var world = SelectedWorld;
            var action = (CancellationToken token) =>
            {
                var addedMaps = new List<Selectable<Map>>();
                var addedStructures = new List<StructureGrid>();
                Parallel.ForEach(settingsList.ToList(), (stuff, state) =>
                {
                    if (token.IsCancellationRequested)
                    {
                        state.Break();
                        return;
                    }
                    var (setting, id) = stuff;
                    var mapData = world.MakeMaps(setting);
                    var maps = ListUtils.Map2D(mapData, x => new Map(id++, x));
                    var import = ListUtils.Flatten(maps).Select(x => new Selectable<Map>(x)).ToList();
                    var structure = new StructureGrid("imagemap:" + setting.Preview.Source.Name, maps)
                    {
                        GlowingFrames = AppSettings.Default.GlowingFrames,
                        InvisibleFrames = AppSettings.Default.InvisibleFrames
                    };
                    lock (settings)
                    {
                        addedMaps.AddRange(import);
                        addedStructures.Add(structure);
                        UiInvoke(() =>
                        {
                            if (!token.IsCancellationRequested)
                                InsertRange(import, ImportingMaps, Sorter);
                        });
                        ImportingStructures.Add(structure);
                        settings.Remove(setting);
                    }
                });
                return (addedMaps, addedStructures);
            };

            _undoHistory.PerformContext(
                ((List<Selectable<Map>> doneMaps, List<StructureGrid> doneStructures)? context) =>
                {
                    var maps = new List<Selectable<Map>>();
                    var structures = new List<StructureGrid>();
                    if (context.HasValue)
                    {
                        maps.AddRange(context.Value.doneMaps);
                        structures.AddRange(context.Value.doneStructures);
                        InsertRange(maps, ImportingMaps, Sorter);
                        ImportingStructures.AddRange(structures);
                    }
                    var source = new CancellationTokenSource();
                    var task = Task.Run(() => action(source.Token));
                    return (source, task, maps, structures);
                },
                context =>
                {
                    var (source, task, doneMaps, doneStructures) = context;
                    source.Cancel();
                    task.Wait();
                    var (maps, structures) = task.Result;
                    doneMaps.AddRange(maps);
                    doneStructures.AddRange(structures);
                    RemoveRange(doneMaps, ImportingMaps);
                    RemoveRange(doneStructures, ImportingStructures);
                    return (doneMaps, doneStructures);
                });
            await Task.CompletedTask;
        }

        public void CloseSessionLock()
        {
            try { _sessionLock?.Unlock(0, 1); } catch { }
            try { _sessionLock?.Close(); } catch { }
            try { _sessionLock?.Dispose(); } catch { }
            _sessionLock = null;
        }
    }

    public enum InsufficientSlotsAction
    {
        Ignore,
        Pause
    }
}
