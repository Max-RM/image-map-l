using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ImageMap.WinForms
{
    public enum ChangeResult
    {
        Confirmed,
        Auto,
        Cancelled
    }

    public sealed class IdChangeForm : Form
    {
        private readonly NumericUpDown _id = new();
        private readonly Panel _warning = new();
        private readonly HashSet<long> _taken;
        private readonly int _count;

        public long ID => (long)_id.Value;
        public ChangeResult Result { get; private set; } = ChangeResult.Cancelled;

        public IdChangeForm(long starting, int count, HashSet<long> taken)
        {
            _taken = taken;
            _count = count;
            Text = "Change map ID";
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

            var row = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false
            };
            row.Controls.Add(new Label
            {
                Text = "map_",
                AutoSize = true,
                Margin = new Padding(0, 8, 5, 0)
            });
            _id.Minimum = 0;
            _id.Maximum = decimal.MaxValue;
            _id.Width = 180;
            _id.DecimalPlaces = 0;
            _id.Value = Math.Max(0, starting);
            _id.ValueChanged += (_, _) => UpdateConflicts();
            row.Controls.Add(_id);
            root.Controls.Add(row);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Margin = new Padding(0, 15, 0, 0)
            };
            var confirm = new Button { Text = "Confirm", AutoSize = true, Padding = new Padding(10, 5, 10, 5) };
            var auto = new Button { Text = "Auto", AutoSize = true, Padding = new Padding(10, 5, 10, 5), Margin = new Padding(5, 0, 0, 0) };
            var cancel = new Button { Text = "Cancel", AutoSize = true, Padding = new Padding(10, 5, 10, 5), Margin = new Padding(5, 0, 0, 0), DialogResult = DialogResult.Cancel };
            confirm.Click += (_, _) => Finish(ChangeResult.Confirmed);
            auto.Click += (_, _) => Finish(ChangeResult.Auto);
            buttons.Controls.Add(confirm);
            buttons.Controls.Add(auto);
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons);

            _warning.AutoSize = true;
            _warning.Margin = new Padding(0, 10, 0, 0);
            var warningLayout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true
            };
            var icon = new PictureBox
            {
                Image = Assets.Warning,
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(18, 18),
                Margin = new Padding(0, 2, 0, 0)
            };
            warningLayout.Controls.Add(icon);
            warningLayout.Controls.Add(new Label
            {
                Text = "This will replace existing maps",
                AutoSize = true,
                Font = new Font("Segoe UI", 11f),
                Margin = new Padding(10, 0, 10, 0)
            });
            _warning.Controls.Add(warningLayout);
            root.Controls.Add(_warning);

            Controls.Add(root);
            AcceptButton = confirm;
            CancelButton = cancel;
            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Result = ChangeResult.Cancelled;
                    Close();
                }
            };
            Shown += (_, _) => { _id.Select(); _id.Focus(); };
            UpdateConflicts();
        }

        private void Finish(ChangeResult result)
        {
            Result = result;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void UpdateConflicts()
        {
            long start = (long)_id.Value;
            bool conflicts = false;
            for (long i = start; i < start + _count; i++)
            {
                if (_taken.Contains(i))
                {
                    conflicts = true;
                    break;
                }
            }
            _warning.Visible = conflicts;
        }
    }
}
