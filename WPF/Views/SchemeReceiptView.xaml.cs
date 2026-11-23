using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class SchemeReceiptView : UserControl
{
    private SchemeReceiptViewModel? _viewModel;

    public SchemeReceiptView()
    {
        InitializeComponent();
        Loaded += SchemeReceiptView_Loaded;
        IsVisibleChanged += SchemeReceiptView_IsVisibleChanged;
        DataContextChanged += SchemeReceiptView_DataContextChanged;
        AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnViewPreviewKeyDown), true);
    }

    private void OnViewPreviewKeyDown(object sender, KeyEventArgs e)
    {
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
            else if (key == Key.P)
            {
                if (_viewModel?.PrintCommand.CanExecute(null) == true)
                {
                    _viewModel.PrintCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            if (e.Key == Key.P)
            {
                if (_viewModel?.PrintCommand.CanExecute(null) == true)
                {
                    _viewModel.PrintCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }

    private void SchemeReceiptView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.RequestFocusAdd -= ViewModel_RequestFocusAdd;
            _viewModel.RequestFocusFirstInput -= ViewModel_RequestFocusFirstInput;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as SchemeReceiptViewModel;
        if (_viewModel != null)
        {
            _viewModel.RequestFocusAdd += ViewModel_RequestFocusAdd;
            _viewModel.RequestFocusFirstInput += ViewModel_RequestFocusFirstInput;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_RequestFocusAdd(object? sender, EventArgs e)
    {
        FocusAddNewButton();
    }

    private void ViewModel_RequestFocusFirstInput(object? sender, EventArgs e)
    {
        FocusFirstInput();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SchemeReceiptViewModel.ToastMessage) ||
            e.PropertyName == nameof(SchemeReceiptViewModel.ToastType))
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

    private void SchemeReceiptView_Loaded(object sender, RoutedEventArgs e)
    {
        FocusAddNewButton();
        UpdateToastVisibility();
    }

    private void SchemeReceiptView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
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

    public void FocusFirstInput()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            // Focus SCode or first field
            Keyboard.Focus(this);
        }));
    }

    public void FocusSaveButton()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            SaveButton?.Focus();
            if (SaveButton != null)
            {
                Keyboard.Focus(SaveButton);
            }
        }));
    }

    private void NarrationTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt)) == 0)
        {
            e.Handled = true;
            FocusSaveButton();
        }
    }
}
