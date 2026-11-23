using System.Threading.Tasks;

namespace ConceptJewelleryPro.WPF.Helpers;

/// <summary>
/// Defines a contract for view models that support on-demand refreshing
/// of lookup data, comboboxes, and dropdown items (e.g. via F9 shortcut).
/// </summary>
public interface IRefreshable
{
    Task RefreshAsync();
}
