using System.Windows.Input;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class MenuEntry
{
    public MenuEntry(string title, string subtitle, string icon)
    {
        Title = title;
        Subtitle = subtitle;
        Icon = icon;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string Icon { get; }

    public ICommand? Command { get; set; }
}
