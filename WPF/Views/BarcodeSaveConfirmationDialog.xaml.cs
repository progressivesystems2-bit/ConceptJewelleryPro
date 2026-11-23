using System.Windows;
using ConceptJewelleryPro.WPF.Helpers;

using System.Windows.Input;
using System.Windows.Threading;

namespace ConceptJewelleryPro.WPF.Views;

public enum BarcodeSaveChoice
{
    Cancel,
    SaveOnly,
    SaveAndPrint
}

public partial class BarcodeSaveConfirmationDialog : Window
{
    public BarcodeSaveChoice UserChoice { get; private set; } = BarcodeSaveChoice.Cancel;

    public BarcodeSaveConfirmationDialog(string barcode = "", string itemName = "")
    {
        InitializeComponent();

        this.SetOwnerSafely();

        if (!string.IsNullOrWhiteSpace(barcode))
        {
            SubtitleText.Text = $"Do you want to save Barcode #{barcode}?";
        }

        if (!string.IsNullOrWhiteSpace(itemName))
        {
            BarcodeDetailText.Text = $"Item: {itemName} | Opening Stock (VouNo: 9)";
        }

        Loaded += (_, _) => FocusDefaultButton();
        ContentRendered += (_, _) => FocusDefaultButton();
    }

    private void FocusDefaultButton()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            SaveAndPrintButton.Focus();
            Keyboard.Focus(SaveAndPrintButton);
        }));
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (SaveOnlyButton.IsKeyboardFocused || SaveOnlyButton.IsFocused)
            {
                ExecuteSaveOnly();
            }
            else if (CancelButton.IsKeyboardFocused || CancelButton.IsFocused)
            {
                ExecuteCancel();
            }
            else
            {
                ExecuteSaveAndPrint();
            }
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            ExecuteCancel();
        }
        else if (e.Key == Key.Right)
        {
            if (SaveAndPrintButton.IsKeyboardFocused || SaveAndPrintButton.IsFocused)
            {
                SaveOnlyButton.Focus();
                Keyboard.Focus(SaveOnlyButton);
                e.Handled = true;
            }
            else if (SaveOnlyButton.IsKeyboardFocused || SaveOnlyButton.IsFocused)
            {
                CancelButton.Focus();
                Keyboard.Focus(CancelButton);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Left)
        {
            if (CancelButton.IsKeyboardFocused || CancelButton.IsFocused)
            {
                SaveOnlyButton.Focus();
                Keyboard.Focus(SaveOnlyButton);
                e.Handled = true;
            }
            else if (SaveOnlyButton.IsKeyboardFocused || SaveOnlyButton.IsFocused)
            {
                SaveAndPrintButton.Focus();
                Keyboard.Focus(SaveAndPrintButton);
                e.Handled = true;
            }
        }
    }

    private void SaveAndPrint_Click(object sender, RoutedEventArgs e)
    {
        ExecuteSaveAndPrint();
    }

    private void SaveOnly_Click(object sender, RoutedEventArgs e)
    {
        ExecuteSaveOnly();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ExecuteCancel();
    }

    private void ExecuteSaveAndPrint()
    {
        UserChoice = BarcodeSaveChoice.SaveAndPrint;
        DialogResult = true;
        Close();
    }

    private void ExecuteSaveOnly()
    {
        UserChoice = BarcodeSaveChoice.SaveOnly;
        DialogResult = true;
        Close();
    }

    private void ExecuteCancel()
    {
        UserChoice = BarcodeSaveChoice.Cancel;
        DialogResult = false;
        Close();
    }

    public static BarcodeSaveChoice Show(string barcode = "", string itemName = "")
    {
        var dialog = new BarcodeSaveConfirmationDialog(barcode, itemName);
        dialog.ShowDialog();
        return dialog.UserChoice;
    }
}
