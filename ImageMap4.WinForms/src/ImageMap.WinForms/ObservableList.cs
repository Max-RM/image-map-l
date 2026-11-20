using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ImageMap4
{
    public sealed class ObservableList<T> : ObservableCollection<T> where T : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? ItemChanged;

        public ObservableList()
        {
            CollectionChanged += WhenCollectionChanged;
        }

        public ObservableList(IEnumerable<T> items) : this()
        {
            AddRange(items);
        }

        public void AddRange(IEnumerable<T> items)
        {
            foreach (var item in items)
                Add(item);
        }

        private void WhenCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (INotifyPropertyChanged item in e.NewItems)
                    item.PropertyChanged += ItemPropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (INotifyPropertyChanged item in e.OldItems)
                    item.PropertyChanged -= ItemPropertyChanged;
            }
        }

        private void ItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            ItemChanged?.Invoke(sender, e);
        }
    }

    public class Selectable<T> : ObservableObject
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public T Item { get; }

        public Selectable(T item, bool selected = false)
        {
            Item = item;
            _isSelected = selected;
            if (Item is INotifyPropertyChanged property)
                property.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
        }
    }
}
