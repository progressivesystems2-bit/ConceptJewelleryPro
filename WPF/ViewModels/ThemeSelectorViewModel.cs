using ConceptJewelleryPro.WPF.Themes;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class ThemeSelectorViewModel : ViewModelBase
{
    private string _selectedThemeId;

    public ThemeSelectorViewModel()
    {
        Themes = ThemeManager.Themes;
        _selectedThemeId = ThemeManager.CurrentThemeId ?? ThemeManager.DefaultThemeId;
    }

    public IReadOnlyList<ThemeInfo> Themes { get; }

    public string SelectedThemeId
    {
        get => _selectedThemeId;
        set
        {
            if (_selectedThemeId == value || string.IsNullOrEmpty(value))
            {
                return;
            }

            ThemeManager.Apply(value);
            SetProperty(ref _selectedThemeId, value);
        }
    }
}
