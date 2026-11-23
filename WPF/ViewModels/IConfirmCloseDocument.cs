namespace ConceptJewelleryPro.WPF.ViewModels;

public interface IConfirmCloseDocument
{
    /// <summary>
    /// Returns true if the tab has entered data or active edits that require confirmation before closing.
    /// </summary>
    bool HasUnsavedChanges();

    /// <summary>
    /// Optional custom message to show in the confirmation dialog.
    /// </summary>
    string? CloseConfirmationMessage => null;
}
