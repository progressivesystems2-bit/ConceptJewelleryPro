using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class TopMenuCategory : INotifyPropertyChanged
{
    private bool _isOpen;

    public TopMenuCategory(string title, string icon, IReadOnlyList<MenuColumn> columns)
    {
        Title = title;
        Icon = icon;
        Columns = columns.Where(c => c.HasItems).ToList();
    }

    public TopMenuCategory(string title, string icon, IReadOnlyList<MenuSection> sections)
        : this(title, icon, sections.Where(s => s.Items.Count > 0).Select(s => new MenuColumn(s)).ToList())
    {
    }

    public string Title { get; }

    public string Icon { get; }

    public IReadOnlyList<MenuColumn> Columns { get; }

    public IReadOnlyList<MenuSection> Sections => Columns.SelectMany(c => c.Sections).ToList();

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (_isOpen != value)
            {
                _isOpen = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
