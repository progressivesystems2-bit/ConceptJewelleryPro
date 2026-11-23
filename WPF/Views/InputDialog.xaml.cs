using System.Windows;

namespace ConceptJewelleryPro.WPF.Views;

public partial class InputDialog : Window
{
    public string InputText => InputTextBox.Text;

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = prompt;
        InputTextBox.Text = defaultValue;
        Loaded += (_, _) => InputTextBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
