using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ImageMap4;

namespace ImageMap.WinForms
{
    public sealed class InventoryPickerForm : Form
    {
        private readonly ComboBox _inventory = new();
        private readonly MainViewModel _parent;

        public IInventory? SelectedInventory => _inventory.SelectedItem as IInventory;

        public InventoryPickerForm(MainViewModel parent)
        {
            _parent = parent;
            Text = "Add maps to inventory";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 14f);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            KeyPreview = true;

            var root = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                Padding = new Padding(20),
                WrapContents = false
            };

            _inventory.DropDownStyle = ComboBoxStyle.DropDownList;
            _inventory.Width = 280;
            _inventory.DisplayMember = nameof(IInventory.Name);
            var list = parent.PlayerList?.ToList() ?? new List<IInventory> { new NoInventory() };
            _inventory.DataSource = list;
            int choice = AppSettings.Default.InventoryChoice;
            if (choice < 0 || choice >= list.Count)
                choice = list.Count > 1 ? 1 : 0;
            if (list.Count > 0)
                _inventory.SelectedIndex = choice;
            _inventory.SelectedIndexChanged += (_, _) =>
                AppSettings.Default.InventoryChoice = _inventory.SelectedIndex;

            root.Controls.Add(_inventory);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Margin = new Padding(0, 15, 0, 0)
            };
            var confirm = new Button { Text = "Confirm", AutoSize = true, Padding = new Padding(10, 5, 10, 5) };
            var cancel = new Button { Text = "Cancel", AutoSize = true, Padding = new Padding(10, 5, 10, 5), Margin = new Padding(5, 0, 0, 0), DialogResult = DialogResult.Cancel };
            confirm.Click += (_, _) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };
            buttons.Controls.Add(confirm);
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons);
            Controls.Add(root);
            AcceptButton = confirm;
            CancelButton = cancel;
        }
    }
}
