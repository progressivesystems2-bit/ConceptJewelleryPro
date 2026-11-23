namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class ModulePlaceholderViewModel
{
    public ModulePlaceholderViewModel(string title, string icon, string message)
    {
        Title = title;
        Icon = icon;
        Message = message;
    }

    public string Title { get; }

    public string Icon { get; }

    public string Message { get; }
}
