using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Ookii.Dialogs.Wpf;

namespace ImageMap4;

public partial class WorldFoldersWindow : Window, INotifyPropertyChanged
{
    public string WindowTitle { get; }
    public string Description { get; }
    public ObservableCollection<WorldSavePath> Items { get; }
    public List<string> SelectedPaths { get; private set; } = new();

    private bool _replaceExisting;
    public bool ReplaceExisting
    {
        get => _replaceExisting;
        set { _replaceExisting = value; OnPropertyChanged(); }
    }

    public WorldFoldersWindow(string title, string description, IEnumerable<WorldSavePath> items, bool replaceExisting)
    {
        WindowTitle = title;
        Description = description;
        Items = new ObservableCollection<WorldSavePath>(items);
        _replaceExisting = replaceExisting;
        InitializeComponent();
        DataContext = this;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new VistaFolderBrowserDialog();
        dialog.Description = "Select a world folder to add";
        dialog.UseDescriptionForTitle = true;
        if (dialog.ShowDialog(this) != true)
            return;
        string path = dialog.SelectedPath.TrimEnd('\\', '/');
        var existing = Items.FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.IsChecked = true;
        }
        else
        {
            string name = System.IO.Path.GetFileName(path);
            Items.Add(new WorldSavePath
            {
                Label = string.IsNullOrEmpty(name) ? path : "Custom: " + name,
                Path = path,
                Kind = WorldSavePathKind.Custom,
                IsCustom = true,
                IsChecked = true
            });
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items)
            item.IsChecked = true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        SelectedPaths = Items.Where(x => x.IsChecked).Select(x => x.Path).ToList();
        if (SelectedPaths.Count == 0)
        {
            MessageBox.Show(this, "Please check at least one folder to use.", "Image Map", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
        Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
