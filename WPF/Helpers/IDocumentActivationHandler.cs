using System.Threading.Tasks;

namespace ConceptJewelleryPro.WPF.Helpers;

/// <summary>
/// Defines lifecycle hooks when a document tab is activated or deactivated in the main window.
/// </summary>
public interface IDocumentActivationHandler
{
    Task OnActivatedAsync();
    Task OnDeactivatedAsync();
}
