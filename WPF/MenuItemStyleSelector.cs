using System.Windows;
using System.Windows.Controls;
using Telerik.Windows.Controls;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF;

public class MenuItemStyleSelector : StyleSelector
{
    public Style TopLevelStyle { get; set; }
    public Style LeafStyle { get; set; }

    public override Style SelectStyle(object item, DependencyObject container)
    {
        return item switch
        {
            MenuSection => TopLevelStyle,
            MenuEntry => LeafStyle,
            _ => base.SelectStyle(item, container)
        };
    }
}