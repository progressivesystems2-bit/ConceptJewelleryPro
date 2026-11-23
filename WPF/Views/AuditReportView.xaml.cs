using System.Windows.Controls;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class AuditReportView : UserControl
{
    public AuditReportView()
    {
        InitializeComponent();
    }

    public AuditReportView(AuditReportViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
