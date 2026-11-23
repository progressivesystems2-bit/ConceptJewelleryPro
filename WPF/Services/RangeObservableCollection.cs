using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ConceptJewelleryPro.WPF.Services;

public class RangeObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

    public RangeObservableCollection() : base() { }

    public RangeObservableCollection(IEnumerable<T> collection) : base(collection) { }

    public void AddRange(IEnumerable<T>? items)
    {
        if (items == null) return;

        _suppressNotification = true;
        try
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
            OnCollectionReset();
        }
    }

    public void ReplaceAll(IEnumerable<T>? items)
    {
        _suppressNotification = true;
        try
        {
            Clear();
            if (items != null)
            {
                foreach (var item in items)
                {
                    Add(item);
                }
            }
        }
        finally
        {
            _suppressNotification = false;
            OnCollectionReset();
        }
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnPropertyChanged(e);
        }
    }

    private void OnCollectionReset()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
