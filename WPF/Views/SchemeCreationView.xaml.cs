using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class SchemeCreationView : UserControl
{
    private SchemeCreationViewModel? _viewModel;

    public SchemeCreationView()
    {
        InitializeComponent();
        Loaded += SchemeCreationView_Loaded;
        IsVisibleChanged += SchemeCreationView_IsVisibleChanged;
        DataContextChanged += SchemeCreationView_DataContextChanged;
        AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnViewPreviewKeyDown), true);
    }

    private void OnViewPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handle Alt shortcuts directly (WPF delivers Alt combinations via e.SystemKey)
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.A)
            {
                if (_viewModel?.NewCommand.CanExecute(null) == true)
                {
                    _viewModel.NewCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (key == Key.S)
            {
                if (_viewModel?.SaveCommand.CanExecute(null) == true)
                {
                    _viewModel.SaveCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (key == Key.M)
            {
                if (_viewModel?.ModifyCommand.CanExecute(null) == true)
                {
                    _viewModel.ModifyCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (key == Key.C)
            {
                if (_viewModel?.CancelCommand.CanExecute(null) == true)
                {
                    _viewModel.CancelCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (key == Key.D)
            {
                if (_viewModel?.DeleteCommand.CanExecute(null) == true)
                {
                    _viewModel.DeleteCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }

    private void SchemeCreationView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.RequestFocusCustomer -= ViewModel_RequestFocusCustomer;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as SchemeCreationViewModel;
        if (_viewModel != null)
        {
            _viewModel.RequestFocusCustomer += ViewModel_RequestFocusCustomer;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_RequestFocusCustomer(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            CustomerComboBox?.Focus();
            if (CustomerComboBox != null)
            {
                Keyboard.Focus(CustomerComboBox);
            }
        }));
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SchemeCreationViewModel.ToastMessage) ||
            e.PropertyName == nameof(SchemeCreationViewModel.ToastType))
        {
            UpdateToastVisibility();
        }
    }

    private void UpdateToastVisibility()
    {
        if (_viewModel == null) return;

        if (!string.IsNullOrEmpty(_viewModel.ToastMessage))
        {
            ToastNotification.Message = _viewModel.ToastMessage;
            ToastNotification.ToastType = _viewModel.ToastType;
            ToastNotification.Show();
        }
        else
        {
            ToastNotification.Hide();
        }
    }

    private void SchemeCreationView_Loaded(object sender, RoutedEventArgs e)
    {
        FocusAddNewButton();
        UpdateToastVisibility();
    }

    private void SchemeCreationView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            FocusAddNewButton();
        }
    }

    public void FocusAddNewButton()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            AddNewButton?.Focus();
            if (AddNewButton != null)
            {
                Keyboard.Focus(AddNewButton);
            }
        }));
    }
}
