using System.Windows.Controls;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class CardAdjustmentReceiptView : UserControl
{
    public CardAdjustmentReceiptView()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            if (DataContext is CardAdjustmentReceiptViewModel vm)
            {
                await vm.InitializeAsync();
            }
        };
    }
}
