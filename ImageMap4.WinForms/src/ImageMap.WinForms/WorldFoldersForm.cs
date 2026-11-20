using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ImageMap4;

namespace ImageMap.WinForms
{
    public sealed class WorldFoldersForm : Form
    {
        private readonly ListView _list = new ListView();
        private readonly CheckBox _replace = new CheckBox();
        private readonly string _kind;

        public List<string> SelectedPaths { get; private set; } = new();
        public bool ReplaceExisting => _replace.Checked;

        public WorldFoldersForm(string title, string description, IEnumerable<WorldSavePath> items, bool replaceExisting, string kind)
        {
            _kind = kind;
            Text = title;
            Width = 720;
            Height = 520;
            MinimumSize = new Size(480, 320);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10f);
            Icon = SystemIcons.Application;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15),
                ColumnCount = 1,
                RowCount = 4
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var descriptionLabel = new Label
            {
                Text = description,
                AutoSize = true,
                MaximumSize = new Size(660, 0),
                Margin = new Padding(0, 0, 0, 10)
            };
            layout.Controls.Add(descriptionLabel, 0, 0);

            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.CheckBoxes = true;
            _list.FullRowSelect = true;
            _list.HideSelection = false;
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.Columns.Add("Folder", 250);
            _list.Columns.Add("Path", 410);
            foreach (var item in items)
            {
                var row = new ListViewItem(item.Label) { Tag = item, Checked = item.IsChecked };
                row.SubItems.Add(item.Path);
                _list.Items.Add(row);
            }
            var border = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1) };
            border.Controls.Add(_list);
            layout.Controls.Add(border, 0, 1);

            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 4,
                Margin = new Padding(0, 12, 0, 0)
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var browse = new Button { Text = "Browse...", AutoSize = true, Padding = new Padding(10, 3, 10, 3) };
            browse.Click += Browse_Click;
            _replace.Text = "Replace current list";
            _replace.AutoSize = true;
            _replace.Checked = replaceExisting;
            _replace.Margin = new Padding(15, 7, 0, 0);
            var selectAll = new Button { Text = "Select All", AutoSize = true, Padding = new Padding(10, 3, 10, 3) };
            selectAll.Click += (s, e) => { foreach (ListViewItem item in _list.Items) item.Checked = true; };
            actions.Controls.Add(browse, 0, 0);
            actions.Controls.Add(_replace, 1, 0);
            actions.Controls.Add(new Panel(), 2, 0);
            actions.Controls.Add(selectAll, 3, 0);
            layout.Controls.Add(actions, 0, 2);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 12, 0, 0)
            };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(15, 3, 15, 3) };
            var ok = new Button { Text = "OK", AutoSize = true, Padding = new Padding(15, 3, 15, 3) };
            ok.Click += Ok_Click;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            layout.Controls.Add(buttons, 0, 3);

            Controls.Add(layout);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void Browse_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select a world folder to add",
                ShowNewFolderButton = false
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            string path = dialog.SelectedPath.TrimEnd('\\', '/');
            foreach (ListViewItem row in _list.Items)
            {
                if (row.Tag is WorldSavePath existing &&
                    string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    row.Checked = true;
                    row.Selected = true;
                    row.EnsureVisible();
                    return;
                }
            }
            string name = Path.GetFileName(path);
            var item = new WorldSavePath
            {
                Label = string.IsNullOrEmpty(name) ? path : "Custom: " + name,
                Path = path,
                Kind = WorldSavePathKind.Custom,
                IsCustom = true,
                IsChecked = true
            };
            var added = new ListViewItem(item.Label) { Tag = item, Checked = true };
            added.SubItems.Add(item.Path);
            _list.Items.Add(added);
            added.EnsureVisible();
        }

        private void Ok_Click(object? sender, EventArgs e)
        {
            SelectedPaths = _list.Items.Cast<ListViewItem>()
                .Where(x => x.Checked && x.Tag is WorldSavePath)
                .Select(x => ((WorldSavePath)x.Tag).Path)
                .ToList();
            if (SelectedPaths.Count == 0)
            {
                MessageBox.Show(this, "Please check at least one folder to use.", "Image Map",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
