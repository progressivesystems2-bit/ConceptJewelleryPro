using System;
using System.Windows;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class Gstr1ExportWindow : Window
{
    public Gstr1ExportWindow(Gstr1ExportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        viewModel.RequestClose += (s, e) => Close();
    }
}
