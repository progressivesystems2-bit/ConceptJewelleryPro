namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class GlobalSearchResultViewModel : ViewModelBase
{
    private string _title = string.Empty;
    private string _detail = string.Empty;
    private string _navigationKey = string.Empty;
    private object? _navigationParameter;
    private string _icon = AppIcons.Dashboard;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Detail
    {
        get => _detail;
        set => SetProperty(ref _detail, value);
    }

    public string NavigationKey
    {
        get => _navigationKey;
        set => SetProperty(ref _navigationKey, value);
    }

    public object? NavigationParameter
    {
        get => _navigationParameter;
        set => SetProperty(ref _navigationParameter, value);
    }

    public string Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public string DisplayText => string.IsNullOrWhiteSpace(Detail) ? Title : $"{Title}: {Detail}";
}
