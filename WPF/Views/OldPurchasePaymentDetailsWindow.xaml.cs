using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Telerik.Windows.Controls;

namespace ConceptJewelleryPro.WPF.Views;

public sealed class OldPurchaseDetailsResult
{
    public string ItemType { get; set; } = string.Empty;
    public string ItemName { get; set; } = "OLD";
    public decimal Wt { get; set; }
    public decimal LessWt { get; set; }
    public decimal GrWt { get; set; }
    public decimal Alloy { get; set; } = 0m;
    public decimal NtWt { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public string Narration { get; set; } = string.Empty;
}

public partial class OldPurchasePaymentDetailsWindow : Window
{
    private bool _isUpdating = true;
    public OldPurchaseDetailsResult? Result { get; private set; }

    public OldPurchasePaymentDetailsWindow(
        string accountName,
        IEnumerable<string> itemTypes,
        decimal initialAmount = 0,
        string? defaultItemType = null,
        decimal initialWt = 0,
        decimal initialLessWt = 0,
        decimal initialAlloy = 0,
        decimal initialRate = 0,
        string? initialNarration = null)
    {
        _isUpdating = true;
        InitializeComponent();

        if (AccountNameLabel != null)
            AccountNameLabel.Text = string.IsNullOrWhiteSpace(accountName) ? "OLD GOLD PURCHASE" : accountName;

        if (ItemTypeComboBox != null)
        {
            var typesList = (itemTypes as IList<string> ?? itemTypes.ToList());
            ItemTypeComboBox.ItemsSource = typesList;

            var targetDefault = !string.IsNullOrWhiteSpace(defaultItemType) ? defaultItemType : "GOLD PRIMARY";

            var selectedType = typesList.FirstOrDefault(t => string.Equals(t?.Trim(), targetDefault.Trim(), StringComparison.OrdinalIgnoreCase))
                               ?? typesList.FirstOrDefault(t => t != null && t.IndexOf("GOLD PRIMARY", StringComparison.OrdinalIgnoreCase) >= 0)
                               ?? typesList.FirstOrDefault(t => string.Equals(t?.Trim(), "GOLD", StringComparison.OrdinalIgnoreCase))
                               ?? targetDefault;

            ItemTypeComboBox.SelectedItem = selectedType;
            ItemTypeComboBox.Text = selectedType;
        }

        if (WtTextBox != null) WtTextBox.Text = initialWt > 0 ? initialWt.ToString("F3", CultureInfo.InvariantCulture) : "";
        if (LessWtTextBox != null) LessWtTextBox.Text = initialLessWt > 0 ? initialLessWt.ToString("F3", CultureInfo.InvariantCulture) : "0.000";
        if (AlloyTextBox != null) AlloyTextBox.Text = initialAlloy > 0 ? initialAlloy.ToString("F2", CultureInfo.InvariantCulture) : "0.00";
        if (RateTextBox != null) RateTextBox.Text = initialRate > 0 ? initialRate.ToString("F2", CultureInfo.InvariantCulture) : "";
        if (AmountTextBox != null) AmountTextBox.Text = initialAmount > 0 ? initialAmount.ToString("F2", CultureInfo.InvariantCulture) : "";
        if (NarrationTextBox != null) NarrationTextBox.Text = initialNarration ?? "";

        // Attach auto select-all on focus to all editable input textboxes
        AttachSelectAllOnFocus(WtTextBox);
        AttachSelectAllOnFocus(LessWtTextBox);
        AttachSelectAllOnFocus(AlloyTextBox);
        AttachSelectAllOnFocus(RateTextBox);
        AttachSelectAllOnFocus(AmountTextBox);
        AttachSelectAllOnFocus(NarrationTextBox);

        _isUpdating = false;

        Recalculate();

        PreviewKeyDown += Window_KeyDown;
        Loaded += (_, _) =>
        {
            if (WtTextBox != null && string.IsNullOrWhiteSpace(WtTextBox.Text))
            {
                WtTextBox.Focus();
                WtTextBox.SelectAll();
            }
            else if (AmountTextBox != null)
            {
                AmountTextBox.Focus();
                AmountTextBox.SelectAll();
            }
        };
    }

    private static void AttachSelectAllOnFocus(TextBox? textBox)
    {
        if (textBox == null) return;
        textBox.GotKeyboardFocus += (s, e) =>
        {
            if (s is TextBox tb) tb.SelectAll();
        };
        textBox.PreviewMouseLeftButtonDown += (s, e) =>
        {
            if (s is TextBox tb && !tb.IsKeyboardFocusWithin)
            {
                tb.Focus();
                tb.SelectAll();
                e.Handled = true;
            }
        };
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        bool isAltS = (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Alt) && (e.Key == Key.S || e.SystemKey == Key.S));
        bool isF10 = (e.Key == Key.F10);

        if (isAltS || isF10)
        {
            if (FocusManager.GetFocusedElement(this) is UIElement focusedUi)
            {
                focusedUi.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
            Ok_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Cancel_Click(sender, e);
            e.Handled = true;
        }
    }

    private decimal ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0m;
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? val :
               (decimal.TryParse(text, out var val2) ? val2 : 0m);
    }

    private void Recalculate(bool fromAmount = false, bool fromRate = false)
    {
        if (_isUpdating) return;
        if (WtTextBox == null || LessWtTextBox == null || AlloyTextBox == null ||
            GrWtTextBox == null || NtWtTextBox == null || RateTextBox == null || AmountTextBox == null)
            return;

        _isUpdating = true;

        try
        {
            var wt = ParseDecimal(WtTextBox?.Text);
            var lessWt = ParseDecimal(LessWtTextBox?.Text);
            var alloy = ParseDecimal(AlloyTextBox?.Text);

            var grWt = Math.Max(0m, wt - lessWt);
            var ntWt = Math.Round(Math.Max(0m, grWt - (grWt * (alloy / 100m))), 3);

            if (GrWtTextBox != null) GrWtTextBox.Text = grWt.ToString("N3");
            if (NtWtTextBox != null) NtWtTextBox.Text = ntWt.ToString("N3");

            if (fromAmount)
            {
                var amt = ParseDecimal(AmountTextBox?.Text);
                if (grWt > 0)
                {
                    var rate = Math.Round(amt / grWt, 2);
                    if (RateTextBox != null) RateTextBox.Text = rate > 0 ? rate.ToString("N2") : "";
                }
            }
            else if (fromRate)
            {
                var rate = ParseDecimal(RateTextBox?.Text);
                var amt = Math.Round(grWt * rate, 2);
                if (AmountTextBox != null) AmountTextBox.Text = amt > 0 ? amt.ToString("N2") : "";
            }
            else
            {
                var rate = ParseDecimal(RateTextBox?.Text);
                var amt = ParseDecimal(AmountTextBox?.Text);

                if (rate > 0)
                {
                    amt = Math.Round(grWt * rate, 2);
                    if (AmountTextBox != null) AmountTextBox.Text = amt > 0 ? amt.ToString("N2") : "";
                }
                else if (amt > 0 && grWt > 0)
                {
                    rate = Math.Round(amt / grWt, 2);
                    if (RateTextBox != null) RateTextBox.Text = rate > 0 ? rate.ToString("N2") : "";
                }
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void Wt_TextChanged(object sender, TextChangedEventArgs e) => Recalculate();
    private void LessWt_TextChanged(object sender, TextChangedEventArgs e) => Recalculate();
    private void Alloy_TextChanged(object sender, TextChangedEventArgs e) => Recalculate();
    private void Rate_TextChanged(object sender, TextChangedEventArgs e) => Recalculate(fromRate: true);
    private void Amount_TextChanged(object sender, TextChangedEventArgs e) => Recalculate(fromAmount: true);
    private void Field_Changed(object sender, RoutedEventArgs e) => Recalculate();

    private void Wt_LostFocus(object sender, RoutedEventArgs e)
    {
        if (WtTextBox == null) return;
        var val = ParseDecimal(WtTextBox.Text);
        WtTextBox.Text = val > 0 ? val.ToString("F3", CultureInfo.InvariantCulture) : "";
    }

    private void LessWt_LostFocus(object sender, RoutedEventArgs e)
    {
        if (LessWtTextBox == null) return;
        var val = ParseDecimal(LessWtTextBox.Text);
        LessWtTextBox.Text = val.ToString("F3", CultureInfo.InvariantCulture);
    }

    private void Alloy_LostFocus(object sender, RoutedEventArgs e)
    {
        if (AlloyTextBox == null) return;
        var val = ParseDecimal(AlloyTextBox.Text);
        AlloyTextBox.Text = val.ToString("F2", CultureInfo.InvariantCulture);
    }

    private void Rate_LostFocus(object sender, RoutedEventArgs e)
    {
        if (RateTextBox == null) return;
        var val = ParseDecimal(RateTextBox.Text);
        RateTextBox.Text = val > 0 ? val.ToString("F2", CultureInfo.InvariantCulture) : "";
    }

    private void Amount_LostFocus(object sender, RoutedEventArgs e)
    {
        if (AmountTextBox == null) return;
        var val = ParseDecimal(AmountTextBox.Text);
        AmountTextBox.Text = val > 0 ? val.ToString("F2", CultureInfo.InvariantCulture) : "";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var wt = ParseDecimal(WtTextBox?.Text);
        var lessWt = ParseDecimal(LessWtTextBox?.Text);
        var alloy = ParseDecimal(AlloyTextBox?.Text);
        var grWt = Math.Max(0m, wt - lessWt);
        var ntWt = Math.Round(Math.Max(0m, grWt - (grWt * (alloy / 100m))), 3);
        var rate = ParseDecimal(RateTextBox?.Text);
        var amount = ParseDecimal(AmountTextBox?.Text);

        Result = new OldPurchaseDetailsResult
        {
            ItemType = ItemTypeComboBox?.Text?.Trim() ?? string.Empty,
            ItemName = "OLD",
            Wt = wt,
            LessWt = lessWt,
            GrWt = grWt,
            Alloy = alloy,
            NtWt = ntWt,
            Rate = rate,
            Amount = amount,
            Narration = NarrationTextBox?.Text?.Trim() ?? string.Empty
        };

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
