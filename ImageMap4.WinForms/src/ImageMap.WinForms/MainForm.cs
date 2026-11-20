using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ImageMap4;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMap.WinForms
{
    public sealed class MainForm : Form
    {
        private readonly MainViewModel _vm = new();
        private readonly TabControl _tabs = new();
        private readonly TabPage _worldsTab = new();
        private readonly TabPage _mapsTab = new();
        private readonly WorldListBox _javaList = new();
        private readonly WorldListBox _bedrockList = new();
        private readonly Label _javaEmpty = new();
        private readonly Label _bedrockEmpty = new();
        private readonly MapListPanel _importList = new(true);
        private readonly MapListPanel _existingList = new(false);
        private readonly Label _importTitle = new();
        private readonly Label _existingTitle = new();
        private readonly Label _transferStatus = new();
        private readonly CheckBox _createMaps = new();
        private readonly CheckBox _createStructures = new();
        private readonly CheckBox _showEmpty = new();
        private readonly Button _openButton = new();
        private readonly Button _sendAll = new();
        private readonly Button _generateStructure = new();
        private readonly PictureBox _sendIcon = new();
        private readonly ContextMenuStrip _worldMenu = new();
        private readonly ContextMenuStrip _importMenu = new();
        private readonly ContextMenuStrip _existingMenu = new();
        private readonly ContextMenuStrip _mapsTabMenu = new();
        private readonly System.Drawing.Image _folderIcon = Assets.CreateFolderIcon();
        private ImportForm? _importWindow;
        private ImageExportForm? _imageWindow;
        private bool _syncingSelection;

        public MainForm()
        {
            Text = "Image Map";
            Width = 940;
            Height = 540;
            MinimumSize = new Size(700, 420);
            Font = new Font("Segoe UI", 13.5f);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            AllowDrop = true;
            TrySetIcon();

            _vm.UiInvoke = action =>
            {
                if (IsDisposed)
                    return;
                if (InvokeRequired)
                    BeginInvoke(action);
                else
                    action();
            };
            _vm.ConfirmInsufficientSlots = ConfirmInsufficientSlots;
            _vm.PropertyChanged += ViewModelChanged;

            BuildTabs();
            BuildMenus();
            BindLists();
            PopulateWorlds();

            KeyDown += MainForm_KeyDown;
            FormClosing += (_, _) =>
            {
                _vm.SelectedWorld = null;
                AppSettings.Default.Save();
            };
            Shown += (_, _) => TryOpenCommandLine();
            DragEnter += WorldsDragEnter;
            DragDrop += WorldsDragDrop;
        }

        private void BuildTabs()
        {
            _tabs.Dock = DockStyle.Fill;
            _tabs.Font = Font;
            _tabs.Padding = new Point(12, 6);
            _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            _tabs.SizeMode = TabSizeMode.Fixed;
            _tabs.ItemSize = new Size(118, 28);
            _tabs.DrawItem += DrawTab;

            _worldsTab.Text = "Worlds";
            _worldsTab.BackColor = Color.White;
            _worldsTab.AllowDrop = true;
            _worldsTab.DragEnter += WorldsDragEnter;
            _worldsTab.DragDrop += WorldsDragDrop;
            _worldsTab.Controls.Add(BuildWorldsPage());

            _mapsTab.Text = "Maps";
            _mapsTab.BackColor = Color.White;
            _mapsTab.Controls.Add(BuildMapsPage());

            _tabs.TabPages.Add(_worldsTab);
            Controls.Add(_tabs);
        }

        private Control BuildWorldsPage()
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _javaList.Dock = DockStyle.Fill;
            _javaList.EmptyText = "";
            _javaList.ContextMenuStrip = _worldMenu;
            _javaList.SelectedIndexChanged += JavaSelected;
            _javaList.MouseDoubleClick += (_, _) => OpenMapsTab();
            _javaEmpty.Text = "Click the button below to select your Java Minecraft 'saves' folder";
            _javaEmpty.ForeColor = Color.Gray;
            _javaEmpty.TextAlign = ContentAlignment.MiddleCenter;
            _javaEmpty.Dock = DockStyle.Fill;
            grid.Controls.Add(WrapList(_javaList, _javaEmpty), 0, 0);

            _bedrockList.Dock = DockStyle.Fill;
            _bedrockList.EmptyText = "";
            _bedrockList.ContextMenuStrip = _worldMenu;
            _bedrockList.SelectedIndexChanged += BedrockSelected;
            _bedrockList.MouseDoubleClick += (_, _) => OpenMapsTab();
            _bedrockEmpty.Text = "Click the button below to select your Bedrock 'minecraftWorlds' folder";
            _bedrockEmpty.ForeColor = Color.Gray;
            _bedrockEmpty.TextAlign = ContentAlignment.MiddleCenter;
            _bedrockEmpty.Dock = DockStyle.Fill;
            grid.Controls.Add(WrapList(_bedrockList, _bedrockEmpty), 1, 0);

            var javaButton = FolderButton("Java Worlds", JavaFolder_Click);
            var bedrockButton = FolderButton("Bedrock Worlds", BedrockFolder_Click);
            grid.Controls.Add(javaButton, 0, 1);
            grid.Controls.Add(bedrockButton, 1, 1);
            return grid;
        }

        private Control BuildMapsPage()
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            grid.Controls.Add(BuildImportHeader(), 0, 0);
            grid.Controls.Add(BuildExistingHeader(), 1, 0);

            _importList.Dock = DockStyle.Fill;
            _importList.EmptyText = "Open, drag, or paste images here to import and convert them to maps.";
            _importList.FilesDropped += files => OpenImages(CollectImageFiles(files).Select(PendingSource.FromPath));
            _importList.DeleteRequested += (_, _) => _vm.DiscardSelected(_vm.ImportingMaps);
            var importHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 1, 0) };
            importHost.Controls.Add(_importList);
            grid.Controls.Add(importHost, 0, 1);

            _existingList.Dock = DockStyle.Fill;
            _existingList.DeleteRequested += (_, _) => _vm.DeleteSelected(_vm.ExistingMaps);
            grid.Controls.Add(_existingList, 1, 1);
            return grid;
        }

        private Control BuildImportHeader()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
                Padding = new Padding(10, 5, 10, 5)
            };
            var top = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _importTitle.Text = "Importing";
            _importTitle.AutoSize = true;
            _importTitle.Margin = new Padding(0, 0, 0, 5);
            var checks = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Anchor = AnchorStyles.Right
            };
            StyleHeaderCheck(_createMaps, "Create Items", 0);
            StyleHeaderCheck(_createStructures, "Create structures", 12);
            _createMaps.Checked = _vm.CreateMaps;
            _createMaps.CheckedChanged += (_, _) => _vm.CreateMaps = _createMaps.Checked;
            _createStructures.CheckedChanged += (_, _) =>
            {
                if (_createStructures.Enabled)
                    _vm.CreateStructuresEffective = _createStructures.Checked;
            };
            new ToolTip().SetToolTip(_createMaps, "Add the imported maps to your inventory as map items. If they do not fit, they are packed into chests automatically.");
            new ToolTip().SetToolTip(_createStructures, "Save a structure block that places the maps in item frames. Requires Bedrock 1.13+ (structuretemplate_ in LevelDB) or Java 1.10+.");
            UpdateStructureToggle();
            checks.Controls.Add(_createMaps);
            checks.Controls.Add(_createStructures);
            top.Controls.Add(_importTitle, 0, 0);
            top.Controls.Add(checks, 1, 0);

            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            _openButton.Text = "  Open";
            _openButton.Image = Assets.Map;
            _openButton.TextImageRelation = TextImageRelation.ImageBeforeText;
            _openButton.AutoSize = true;
            _openButton.Padding = new Padding(10, 5, 15, 5);
            _openButton.Click += (_, _) => OpenButton_Click();
            _sendAll.AutoSize = true;
            _sendAll.Padding = new Padding(8, 5, 8, 5);
            _sendAll.Margin = new Padding(10, 0, 0, 0);
            _sendAll.Click += (_, _) => _vm.TransferAll();
            _sendIcon.Size = new Size(18, 18);
            _sendIcon.SizeMode = PictureBoxSizeMode.Zoom;
            _sendIcon.Margin = new Padding(0, 6, 4, 0);
            var sendHost = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(10, 0, 0, 0) };
            sendHost.Controls.Add(_sendIcon);
            sendHost.Controls.Add(_sendAll);
            _sendAll.Text = "Send All to World";
            buttons.Controls.Add(_openButton);
            buttons.Controls.Add(sendHost);

            _transferStatus.AutoSize = true;
            _transferStatus.ForeColor = Color.Gray;
            _transferStatus.MaximumSize = new Size(420, 0);
            _transferStatus.Margin = new Padding(0, 5, 0, 0);

            panel.Controls.Add(top);
            panel.Controls.Add(buttons);
            panel.Controls.Add(_transferStatus);
            UpdateImportChrome();
            return panel;
        }

        private Control BuildExistingHeader()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
                Padding = new Padding(10, 5, 10, 5)
            };
            var top = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _existingTitle.Text = "In World";
            _existingTitle.AutoSize = true;
            _existingTitle.Margin = new Padding(0, 0, 0, 5);
            StyleHeaderCheck(_showEmpty, "Show empty maps", 0);
            _showEmpty.Checked = _vm.ShowEmptyMaps;
            _showEmpty.CheckedChanged += (_, _) =>
            {
                _vm.ShowEmptyMaps = _showEmpty.Checked;
                _existingList.SetShowEmpty(_showEmpty.Checked);
                UpdateExistingChrome();
            };
            top.Controls.Add(_existingTitle, 0, 0);
            top.Controls.Add(_showEmpty, 1, 0);

            _generateStructure.Text = "Generate Structure";
            _generateStructure.AutoSize = true;
            _generateStructure.Padding = new Padding(15, 5, 15, 5);
            _generateStructure.Click += (_, _) => GenerateStructure();
            panel.Controls.Add(top);
            panel.Controls.Add(_generateStructure);
            UpdateExistingChrome();
            return panel;
        }

        private void BindLists()
        {
            _importList.ContextMenuStrip = _importMenu;
            _existingList.ContextMenuStrip = _existingMenu;
            _importList.Bind(_vm.ImportingMaps);
            _existingList.Bind(_vm.ExistingMaps);
            _existingList.SetShowEmpty(_vm.ShowEmptyMaps);
            _vm.ImportingMaps.CollectionChanged += (_, _) => BeginInvokeIfNeeded(UpdateImportChrome);
            _vm.ExistingMaps.CollectionChanged += (_, _) => BeginInvokeIfNeeded(UpdateExistingChrome);
            _vm.ImportingMaps.ItemChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Map.ID))
                    BeginInvokeIfNeeded(UpdateConflicts);
            };
            _vm.ExistingMaps.ItemChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Map.ID))
                    BeginInvokeIfNeeded(UpdateConflicts);
            };
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.ConflictingIDs))
                    BeginInvokeIfNeeded(UpdateConflicts);
            };
        }

        private void BuildMenus()
        {
            _worldMenu.Items.Add("Open folder", null, (_, _) =>
            {
                var world = SelectedListWorld();
                if (world != null)
                    OpenFolder(world.Folder);
            });

            var importPreview = _importMenu.Items.Add("Preview", null, (_, _) => Preview(_importList.FirstSelected));
            var importChange = _importMenu.Items.Add("Change ID", null, (_, _) => ChangeIds(_vm.ImportingMaps));
            var importDiscard = _importMenu.Items.Add("Discard", null, (_, _) => _vm.DiscardSelected(_vm.ImportingMaps));
            var importExport = _importMenu.Items.Add("Export image", null, (_, _) => ExportImage(_vm.ImportingMaps));
            _importMenu.Opening += (_, _) =>
            {
                bool any = _importList.AnySelected;
                importPreview.Enabled = _importList.FirstSelected != null;
                importChange.Enabled = any;
                importDiscard.Enabled = any;
                importExport.Enabled = any;
            };

            var existingPreview = _existingMenu.Items.Add("Preview", null, (_, _) => Preview(_existingList.FirstSelected));
            var existingChange = _existingMenu.Items.Add("Change ID", null, (_, _) => ChangeIds(_vm.ExistingMaps));
            var openFile = new ToolStripMenuItem("Open file");
            openFile.Click += (_, _) => OpenMapFile(_existingList.FirstSelected);
            _existingMenu.Items.Add(openFile);
            var existingDelete = _existingMenu.Items.Add("Delete", null, (_, _) => _vm.DeleteSelected(_vm.ExistingMaps));
            var existingExport = _existingMenu.Items.Add("Export image", null, (_, _) => ExportImage(_vm.ExistingMaps));
            var existingInventory = _existingMenu.Items.Add("Add to inventory", null, (_, _) => AddToInventory());
            _existingMenu.Opening += (_, _) =>
            {
                bool any = _existingList.AnySelected;
                existingPreview.Enabled = _existingList.FirstSelected != null;
                existingChange.Enabled = any;
                openFile.Enabled = any && _vm.SelectedWorld is JavaWorld && _existingList.FirstSelected != null;
                existingDelete.Enabled = any;
                existingExport.Enabled = any;
                existingInventory.Enabled = any;
            };

            _mapsTabMenu.Items.Add("Close", null, (_, _) => CloseMapsTab());
            _tabs.MouseUp += (_, e) =>
            {
                if (e.Button != MouseButtons.Right)
                    return;
                for (int i = 0; i < _tabs.TabCount; i++)
                {
                    if (_tabs.GetTabRect(i).Contains(e.Location) && _tabs.TabPages[i] == _mapsTab)
                        _mapsTabMenu.Show(_tabs, e.Location);
                }
            };
        }

        private void ViewModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            BeginInvokeIfNeeded(() =>
            {
                if (e.PropertyName == nameof(MainViewModel.SelectedWorld) ||
                    e.PropertyName == nameof(MainViewModel.StructuresAvailable) ||
                    e.PropertyName == nameof(MainViewModel.CreateStructuresEffective))
                {
                    if (e.PropertyName == nameof(MainViewModel.SelectedWorld))
                        OnSelectedWorldChanged();
                    else
                        UpdateStructureToggle();
                }
                if (e.PropertyName == nameof(MainViewModel.TransferStatus))
                {
                    _transferStatus.Text = _vm.TransferStatus ?? "";
                    _transferStatus.Visible = !string.IsNullOrEmpty(_vm.TransferStatus);
                }
            });
        }

        private void OnSelectedWorldChanged()
        {
            if (_vm.SelectedWorld == null)
            {
                Text = "Image Map";
                if (_tabs.TabPages.Contains(_mapsTab))
                    _tabs.TabPages.Remove(_mapsTab);
                _tabs.SelectedTab = _worldsTab;
            }
            else
            {
                Text = "Image Map – " + _vm.SelectedWorld.Name;
                if (!_tabs.TabPages.Contains(_mapsTab))
                    _tabs.TabPages.Add(_mapsTab);
                UpdateExistingChrome();
            }
            UpdateStructureToggle();
        }

        private void UpdateStructureToggle()
        {
            bool available = _vm.StructuresAvailable;
            _createStructures.Enabled = available;
            _createStructures.Checked = _vm.CreateStructuresEffective;
        }

        private void PopulateWorlds()
        {
            ReplaceWorldItems(_javaList, _vm.JavaWorlds);
            ReplaceWorldItems(_bedrockList, _vm.BedrockWorlds);
            _javaEmpty.Visible = _javaList.Items.Count == 0;
            _bedrockEmpty.Visible = _bedrockList.Items.Count == 0;
        }

        private static void ReplaceWorldItems(WorldListBox list, IEnumerable<World> worlds)
        {
            list.BeginUpdate();
            foreach (var item in list.Items.OfType<WorldItem>())
                item.Dispose();
            list.Items.Clear();
            foreach (var world in worlds)
                list.Items.Add(new WorldItem(world));
            list.EndUpdate();
        }

        private void JavaSelected(object? sender, EventArgs e)
        {
            if (_syncingSelection || _javaList.SelectedItem is not WorldItem item)
                return;
            _syncingSelection = true;
            _bedrockList.SelectedIndex = -1;
            _syncingSelection = false;
            if (!TryOpenWorld(item.World))
                _javaList.SelectedIndex = -1;
        }

        private void BedrockSelected(object? sender, EventArgs e)
        {
            if (_syncingSelection || _bedrockList.SelectedItem is not WorldItem item)
                return;
            _syncingSelection = true;
            _javaList.SelectedIndex = -1;
            _syncingSelection = false;
            if (!TryOpenWorld(item.World))
                _bedrockList.SelectedIndex = -1;
        }

        private static void StyleHeaderCheck(CheckBox box, string text, int leftMargin)
        {
            box.Text = text;
            box.AutoSize = false;
            box.Height = 24;
            box.Width = Math.Max(140, TextRenderer.MeasureText(text, new Font("Segoe UI", 12f)).Width + 24);
            box.Font = new Font("Segoe UI", 12f);
            box.CheckAlign = ContentAlignment.MiddleLeft;
            box.TextAlign = ContentAlignment.MiddleLeft;
            box.Padding = Padding.Empty;
            box.Margin = new Padding(leftMargin, 0, 0, 0);
            box.FlatStyle = FlatStyle.System;
        }

        private void TrySetIcon()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(path))
                    Icon = new Icon(path);
                else
                    Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }
        }

        private bool TryOpenWorld(World world)
        {
            if (world is JavaWorld java && java.Version == null)
            {
                _vm.SelectedWorld = null;
                return false;
            }
            if (world is BedrockWorld bedrock && bedrock.Version == null)
            {
                _vm.SelectedWorld = null;
                return false;
            }
            if (MainViewModel.SameWorld(_vm.SelectedWorld, world))
            {
                OpenMapsTab();
                return true;
            }
            try
            {
                OpenMapsTab();
                _vm.SelectedWorld = world;
                return true;
            }
            catch (Exception ex)
            {
                CrashLog.Error("Open world", ex);
                MessageBox.Show(this, ex.ToString(), "Image Map", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            _vm.SelectedWorld = null;
            return false;
        }

        private bool TryOpenWorldFolder(string folder)
        {
            if (!Directory.Exists(folder) || !File.Exists(Path.Combine(folder, "level.dat")))
                return false;
            try
            {
                World world = Directory.Exists(Path.Combine(folder, "db"))
                    ? new BedrockWorld(folder)
                    : new JavaWorld(folder);
                return TryOpenWorld(world);
            }
            catch (Exception ex)
            {
                CrashLog.Error("Open world folder " + folder, ex);
                MessageBox.Show(this, ex.ToString(), "Image Map", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void OpenMapsTab()
        {
            if (!_tabs.TabPages.Contains(_mapsTab))
                _tabs.TabPages.Add(_mapsTab);
            _tabs.SelectedTab = _mapsTab;
        }

        private void CloseMapsTab()
        {
            _vm.SelectedWorld = null;
            _javaList.SelectedIndex = -1;
            _bedrockList.SelectedIndex = -1;
            _tabs.SelectedTab = _worldsTab;
        }

        private void JavaFolder_Click(object? sender, EventArgs e)
        {
            AppSettings.Default.JavaFolders ??= new StringCollection();
            PickWorldFolders(AppSettings.Default.JavaFolders, WorldSavePathRegistry.GetJavaPaths,
                "Java worlds", "Check the folder(s) where your Java worlds are saved.");
        }

        private void BedrockFolder_Click(object? sender, EventArgs e)
        {
            AppSettings.Default.BedrockFolders ??= new StringCollection();
            PickWorldFolders(AppSettings.Default.BedrockFolders, WorldSavePathRegistry.GetBedrockPaths,
                "Bedrock worlds", "Check the folder(s) where your Bedrock worlds are saved. All folders found on this computer are listed.");
        }

        private void PickWorldFolders(
            StringCollection folders,
            Func<IEnumerable<string>, List<WorldSavePath>> discover,
            string title,
            string description)
        {
            bool adding = (ModifierKeys & Keys.Shift) != 0;
            var configured = folders.Cast<string>().ToList();
            var candidates = discover(configured);
            foreach (var candidate in candidates)
            {
                candidate.IsChecked = adding || configured.Any(x =>
                    string.Equals(Environment.ExpandEnvironmentVariables(x).TrimEnd('\\', '/'), candidate.Path, StringComparison.OrdinalIgnoreCase));
            }
            using var form = new WorldFoldersForm(title, description, candidates, !adding, title);
            if (form.ShowDialog(this) != DialogResult.OK)
                return;
            if (form.ReplaceExisting)
                folders.Clear();
            foreach (var path in form.SelectedPaths)
            {
                if (!folders.Cast<string>().Any(x => string.Equals(Environment.ExpandEnvironmentVariables(x).TrimEnd('\\', '/'), path, StringComparison.OrdinalIgnoreCase)))
                    folders.Add(path);
            }
            AppSettings.Default.Save();
            _vm.RefreshWorlds();
            PopulateWorlds();
        }

        private void OpenButton_Click()
        {
            using var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Select images to import",
                Filter = "Image Files|*.png; *.bmp; *.jpg; *.jpeg; *.gif|All Files|*"
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                OpenImages(dialog.FileNames.Select(PendingSource.FromPath));
        }

        private void OpenImages(IEnumerable<PendingSource> images)
        {
            var list = images.ToList();
            if (list.Count == 0)
                return;
            if (_importWindow == null || _importWindow.IsDisposed)
            {
                _importWindow = new ImportForm();
                _importWindow.Owner = this;
                _importWindow.ViewModel.JavaMode = _vm.SelectedWorld is JavaWorld;
                _importWindow.ViewModel.OnConfirmed += (_, settings) => _ = _vm.AddImports(settings);
            }
            _importWindow.Show();
            _importWindow.Activate();
            _importWindow.ViewModel.AddImages(list);
        }

        private void GenerateStructure()
        {
            if (_vm.SelectedWorld == null || !_vm.SelectedWorld.SupportsStructures)
                return;
            var maps = _existingList.SelectedMaps.ToList();
            if (maps.Count == 0)
                maps = _existingList.Maps.ToList();
            if (maps.Count == 0)
                return;
            using var form = new StructureForm(_vm.SelectedWorld, maps);
            if (form.ShowDialog(this) != DialogResult.OK || form.Structure == null)
                return;
            try
            {
                _vm.SelectedWorld.AddStructures(new[] { form.Structure }, form.Inventory ?? new NoInventory());
            }
            catch (Exception ex)
            {
                CrashLog.Error("Generate structure", ex);
                MessageBox.Show(this, ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Preview(Map? map)
        {
            if (map == null)
                return;
            using var form = new PreviewForm(map);
            form.ShowDialog(this);
        }

        private void ChangeIds(ObservableList<Selectable<Map>> source)
        {
            var selected = source.Where(x => x.IsSelected).ToList();
            if (selected.Count == 0)
                return;
            var taken = source.Except(selected).Select(x => x.Item.ID).ToHashSet();
            using var form = new IdChangeForm(selected[0].Item.ID, selected.Count, taken);
            if (form.ShowDialog(this) != DialogResult.OK)
                return;
            if (form.Result == ChangeResult.Confirmed)
                _vm.ChangeIDs(source, selected, form.ID);
            else if (form.Result == ChangeResult.Auto)
                _vm.AutoIDs(source, selected);
        }

        private void ExportImage(ObservableList<Selectable<Map>> source)
        {
            var selected = source.Where(x => x.IsSelected).ToList();
            if (selected.Count == 0)
                return;
            if (selected.Count == 1)
            {
                using var dialog = new SaveFileDialog
                {
                    Title = "Export the image",
                    Filter = "Image Files|*.png;|All Files|*",
                    FileName = $"map_{selected[0].Item.ID}.png"
                };
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    ImageUtils.SavePng(selected[0].Item.Data.Image, dialog.FileName);
                return;
            }
            if (_imageWindow == null || _imageWindow.IsDisposed)
            {
                _imageWindow = new ImageExportForm(new ImageViewModel(new GridMakerViewModel(_vm, source)));
                _imageWindow.Owner = this;
            }
            _imageWindow.Show();
            _imageWindow.Activate();
        }

        private void AddToInventory()
        {
            var selected = _vm.ExistingMaps.Where(x => x.IsSelected).ToList();
            if (selected.Count == 0 || _vm.SelectedWorld == null)
                return;
            using var form = new InventoryPickerForm(_vm);
            if (form.ShowDialog(this) != DialogResult.OK)
                return;
            var inventory = form.SelectedInventory;
            if (inventory is NoInventory || inventory == null)
            {
                MessageBox.Show(this, "No inventory was selected.", "Image Map",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                var world = _vm.SelectedWorld;
                var free = inventory.FreeSlots;
                var leaves = selected.Select(x => world.MakeMapLeaf(x.Item.ID)).ToList();
                var packed = leaves.Count > free
                    ? world.PackInventoryItems(leaves, InventoryMode.Chests)
                    : leaves;
                inventory.AddItems(packed);
                int added = Math.Min(packed.Count, free);
                int after = inventory.FreeSlots;
                _vm.TransferStatus = free <= 0
                    ? $"No free slots in {inventory.Name}."
                    : after >= free
                        ? $"Could not write to {inventory.Name}; close the world in Minecraft and try again."
                        : added < packed.Count
                            ? $"Added {added} of {packed.Count} map item(s) to {inventory.Name}; not enough free slots."
                            : $"Added {added} map item(s) to {inventory.Name}.";
            }
            catch (Exception ex)
            {
                CrashLog.Error("Add to inventory", ex);
                _vm.TransferStatus = "Could not write: " + ex.Message;
            }
        }

        private void OpenMapFile(Map? map)
        {
            if (map == null || _vm.SelectedWorld is not JavaWorld world || world.Version == null)
                return;
            string path = world.Version.MapFileLocation(world.Folder, map.ID);
            Process.Start("explorer.exe", "/select, \"" + path.Replace('/', '\\') + "\"");
        }

        private void OpenFolder(string folder)
        {
            Process.Start("explorer.exe", "\"" + folder + "\"");
        }

        private void MainForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                Paste();
                e.Handled = true;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.Z)
            {
                _vm.Redo();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Z)
            {
                _vm.Undo();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                if (_importList.ContainsFocus || _importList.Focused)
                    _vm.DiscardSelected(_vm.ImportingMaps);
                else if (_existingList.ContainsFocus || _existingList.Focused)
                    _vm.DeleteSelected(_vm.ExistingMaps);
                e.Handled = true;
            }
        }

        private void Paste()
        {
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList().Cast<string>();
                    OpenImages(CollectImageFiles(files).Select(PendingSource.FromPath));
                    return;
                }
                if (Clipboard.ContainsImage())
                {
                    using var bitmap = new Bitmap(Clipboard.GetImage());
                    using var stream = new MemoryStream();
                    bitmap.Save(stream, ImageFormat.Png);
                    stream.Position = 0;
                    var bytes = stream.ToArray();
                    OpenImages(new[]
                    {
                        new PendingSource(() => ImageUtils.LoadRgba(bytes), "Pasted image")
                    });
                }
            }
            catch (Exception ex)
            {
                CrashLog.Error("Paste", ex);
            }
        }

        private void WorldsDragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void WorldsDragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 0)
                return;
            if (_tabs.SelectedTab == _mapsTab)
            {
                OpenImages(CollectImageFiles(files).Select(PendingSource.FromPath));
                return;
            }
            if (TryOpenWorldFolder(files[0]))
                OpenMapsTab();
        }

        private void TryOpenCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length <= 1)
                return;
            if (TryOpenWorldFolder(args[1]))
                OpenMapsTab();
            if (args.Length > 2)
                OpenImages(args.Skip(2).Select(PendingSource.FromPath));
        }

        private void UpdateImportChrome()
        {
            int count = _vm.ImportingMaps.Count;
            _importTitle.Text = count == 0 ? "Importing" : $"Importing – {count} maps";
            _sendAll.Visible = count > 0;
            _sendIcon.Visible = count > 0;
            _sendAll.Parent!.Visible = count > 0;
            _transferStatus.Text = _vm.TransferStatus ?? "";
            _transferStatus.Visible = !string.IsNullOrEmpty(_vm.TransferStatus);
            UpdateConflicts();
        }

        private void UpdateExistingChrome()
        {
            int count = _existingList.Maps.Count;
            _existingTitle.Text = $"In World – {count} maps";
            _generateStructure.Visible = count > 0 && (_vm.SelectedWorld?.SupportsStructures ?? false);
        }

        private void UpdateConflicts()
        {
            _importList.SetConflicts(_vm.ConflictingIDs);
            _existingList.SetConflicts(_vm.ConflictingIDs);
            if (_vm.ConflictingIDs.Count > 0)
            {
                _sendIcon.Image = Assets.Warning;
                new ToolTip().SetToolTip(_sendIcon, _vm.ConflictingIDs.Count + " maps will be overwritten");
            }
            else
            {
                _sendIcon.Image = Assets.Checkmark;
                new ToolTip().SetToolTip(_sendIcon, "");
            }
        }

        private InsufficientSlotsAction ConfirmInsufficientSlots(int needed, int free)
        {
            var result = MessageBox.Show(this,
                $"Not enough free inventory slots.\n\nNeeded: {needed}\nFree: {free}\n\n" +
                "Yes — ignore and continue: the map data will still be saved, but the inventory will not be changed.\n" +
                "No — pause: nothing is changed and your imported maps stay in the list.",
                "Image Map", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            return result == DialogResult.Yes ? InsufficientSlotsAction.Ignore : InsufficientSlotsAction.Pause;
        }

        private World? SelectedListWorld()
        {
            if (_javaList.SelectedItem is WorldItem java)
                return java.World;
            if (_bedrockList.SelectedItem is WorldItem bedrock)
                return bedrock.World;
            return null;
        }

        private void DrawTab(object? sender, DrawItemEventArgs e)
        {
            var page = _tabs.TabPages[e.Index];
            var bounds = e.Bounds;
            bool selected = e.Index == _tabs.SelectedIndex;
            using var background = new SolidBrush(selected ? Color.White : SystemColors.Control);
            e.Graphics.FillRectangle(background, bounds);
            Image? icon = page == _worldsTab ? Assets.World : Assets.Map;
            int x = bounds.X + 8;
            int y = bounds.Y + (bounds.Height - 18) / 2;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.DrawImage(icon, new Rectangle(x, y, 18, 18));
            TextRenderer.DrawText(e.Graphics, page.Text, Font, new Point(x + 28, bounds.Y + 4), Color.Black);
        }

        private Button FolderButton(string text, EventHandler click)
        {
            var button = new Button
            {
                Text = "  " + text,
                Image = _folderIcon,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Dock = DockStyle.Fill,
                Height = 36,
                Padding = new Padding(5)
            };
            button.Click += click;
            return button;
        }

        private static Panel WrapList(Control list, Label empty)
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            list.Dock = DockStyle.Fill;
            empty.Dock = DockStyle.Fill;
            panel.Controls.Add(list);
            panel.Controls.Add(empty);
            empty.BringToFront();
            return panel;
        }

        private void BeginInvokeIfNeeded(Action action)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }

        private static IEnumerable<string> CollectImageFiles(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                if (File.Exists(file))
                    yield return file;
                else if (Directory.Exists(file))
                {
                    foreach (var nested in Directory.GetFiles(file))
                        yield return nested;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _folderIcon.Dispose();
            base.Dispose(disposing);
        }
    }
}
