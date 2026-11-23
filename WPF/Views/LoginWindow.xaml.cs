using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.WPF.ViewModels;
using Telerik.Windows.Controls;

namespace ConceptJewelleryPro.WPF.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (DataContext is LoginViewModel viewModel)
        {
            SqlPasswordBox.Password = viewModel.ConnectionSettings.SqlPassword;
        }
        UsernameBox.Focus();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
        {
            viewModel.Password = PasswordBox.Password;
        }
    }

    private void SqlPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
        {
            viewModel.ConnectionSettings.SqlPassword = SqlPasswordBox.Password;
        }
    }

    private void BrowseDatabaseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Microsoft Access database file",
            Filter = "Access Database (*.accdb;*.mdb)|*.accdb;*.mdb|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true && DataContext is LoginViewModel viewModel)
        {
            viewModel.ConnectionSettings.AccessDatabasePath = dialog.FileName;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
