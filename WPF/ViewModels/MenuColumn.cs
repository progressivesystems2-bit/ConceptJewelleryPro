namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class MenuColumn
{
    public MenuColumn(params MenuSection[] sections)
    {
        Sections = sections.Where(s => s != null && s.Items.Count > 0).ToList();
    }

    public MenuColumn(IEnumerable<MenuSection> sections)
    {
        Sections = sections.Where(s => s != null && s.Items.Count > 0).ToList();
    }

    public IReadOnlyList<MenuSection> Sections { get; }

    public bool HasItems => Sections.Count > 0;
}
