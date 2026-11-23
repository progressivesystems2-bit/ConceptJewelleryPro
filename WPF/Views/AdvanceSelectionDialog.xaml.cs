using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ConceptJewelleryPro.WPF.Views;

public partial class AdvanceSelectionDialog : Window
{
    public string? SelectedOption { get; private set; }

    public AdvanceSelectionDialog(string accountName, IEnumerable<string> options, string? initialSelectedOption = null)
    {
        InitializeComponent();
        AccountNameText.Text = string.IsNullOrWhiteSpace(accountName) ? "Debtors Account" : accountName;
        
        var optionsList = options.ToList();
        AdvanceCategoryComboBox.ItemsSource = optionsList;

        if (!string.IsNullOrWhiteSpace(initialSelectedOption) && optionsList.Contains(initialSelectedOption))
        {
            AdvanceCategoryComboBox.SelectedItem = initialSelectedOption;
        }
        else if (optionsList.Count > 0)
        {
            AdvanceCategoryComboBox.SelectedIndex = 0;
        }

        Loaded += OnLoaded;
        KeyDown += AdvanceSelectionDialog_KeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            AdvanceCategoryComboBox.Focus();
            AdvanceCategoryComboBox.IsDropDownOpen = true;
        }));
    }

    private void AdvanceCategoryComboBox_GotFocus(object sender, RoutedEventArgs e)
    {
        AdvanceCategoryComboBox.IsDropDownOpen = true;
    }

    private void AdvanceCategoryComboBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ConfirmSelection();
            return;
        }

        if (!AdvanceCategoryComboBox.IsDropDownOpen &&
            e.Key != Key.Escape &&
            e.Key != Key.Tab)
        {
            AdvanceCategoryComboBox.IsDropDownOpen = true;
        }
    }

    private void AdvanceSelectionDialog_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ConfirmSelection();
        }
    }

    private void ConfirmSelection()
    {
        var text = !string.IsNullOrWhiteSpace(AdvanceCategoryComboBox.Text)
            ? AdvanceCategoryComboBox.Text
            : AdvanceCategoryComboBox.SelectedItem?.ToString();

        if (!string.IsNullOrWhiteSpace(text))
        {
            SelectedOption = text.Trim();
            DialogResult = true;
            Close();
        }
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
