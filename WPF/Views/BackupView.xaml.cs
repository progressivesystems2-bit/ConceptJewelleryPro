using System.Windows;
using System.Windows.Controls;
using ConceptJewelleryPro.Application.Services;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class BackupView : UserControl
{
    public BackupView()
    {
        InitializeComponent();
    }

    private void OnAccessRadioChecked(object sender, RoutedEventArgs e)
    {
        if (DataContext is BackupViewModel vm)
        {
            vm.SelectedProviderIndex = (int)DatabaseEngineType.MicrosoftAccess;
        }
    }

    private void OnSqlRadioChecked(object sender, RoutedEventArgs e)
    {
        if (DataContext is BackupViewModel vm)
        {
            vm.SelectedProviderIndex = (int)DatabaseEngineType.SqlServer;
        }
    }
}
