namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class MenuSection
{
    public MenuSection(string title, string icon, IReadOnlyList<MenuEntry> items, string subtitle = "")
    {
        Title = title;
        Icon = icon;
        Items = items;
        Subtitle = subtitle;
    }

    public MenuSection(string title, string subtitle, string icon, IReadOnlyList<MenuEntry> items)
    {
        Title = title;
        Subtitle = subtitle;
        Icon = icon;
        Items = items;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string Icon { get; }

    public IReadOnlyList<MenuEntry> Items { get; }
}
