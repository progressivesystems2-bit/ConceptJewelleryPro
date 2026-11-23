using System.ComponentModel;
using System.Windows.Controls;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.ViewModels;
using Telerik.Windows.Data;

namespace ConceptJewelleryPro.WPF.Views;

public partial class TrialBalanceView : UserControl
{
    public TrialBalanceView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TrialBalanceViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is TrialBalanceViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            ApplyGroupView(newVm.ViewMode);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrialBalanceViewModel.ViewMode) && sender is TrialBalanceViewModel vm)
        {
            ApplyGroupView(vm.ViewMode);
        }
    }

    private void ApplyGroupView(TrialBalanceViewMode viewMode)
    {
        if (TrialBalanceGrid == null) return;

        TrialBalanceGrid.GroupDescriptors.Clear();
        if (viewMode == TrialBalanceViewMode.GroupSummary)
        {
            TrialBalanceGrid.GroupDescriptors.Add(new GroupDescriptor
            {
                Member = "HeadCategory",
                SortDirection = ListSortDirection.Ascending
            });
        }
    }
}
