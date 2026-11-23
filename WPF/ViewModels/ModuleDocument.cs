namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class ModuleDocument : ViewModelBase
{
    private string _title;
    private string _icon;
    private object _content;

    public ModuleDocument(string title, string icon, object content)
    {
        _title = title;
        _icon = icon;
        _content = content;
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public object Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }
}
