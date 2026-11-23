using System.Windows;
using System.Windows.Controls;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class SoftwareUpdateView : UserControl
{
    private bool _hasAutoChecked;

    public SoftwareUpdateView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_hasAutoChecked) return;
        _hasAutoChecked = true;

        if (DataContext is SoftwareUpdateViewModel vm)
        {
            _ = vm.CheckForUpdatesAsync();
        }
    }
}
