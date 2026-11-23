using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class VoucherEntryView : UserControl
{
    private VoucherEntryViewModel? _viewModel;

    public VoucherEntryView()
    {
        InitializeComponent();
        Loaded += VoucherEntryView_Loaded;
        IsVisibleChanged += VoucherEntryView_IsVisibleChanged;
        DataContextChanged += VoucherEntryView_DataContextChanged;
        AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnViewPreviewKeyDown), true);
    }

    private void OnViewPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handle Alt shortcuts directly
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.A)
            {
                if (_viewModel?.NewVoucherCommand.CanExecute(null) == true)
                {
                    _viewModel.NewVoucherCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (key == Key.S)
            {
                if (_viewModel?.SaveVoucherCommand.CanExecute(null) == true)
                {
                    _viewModel.SaveVoucherCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (key == Key.M)
            {
                if (_viewModel?.ModifyVoucherCommand.CanExecute(null) == true)
                {
                    _viewModel.ModifyVoucherCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (key == Key.C)
            {
                if (_viewModel?.CancelVoucherCommand.CanExecute(null) == true)
                {
                    _viewModel.CancelVoucherCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (key == Key.D)
            {
                if (_viewModel?.DeleteVoucherCommand.CanExecute(null) == true)
                {
                    _viewModel.DeleteVoucherCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (key == Key.P)
            {
                if (_viewModel?.PrintVoucherCommand.CanExecute(null) == true)
                {
                    _viewModel.PrintVoucherCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            if (e.Key == Key.P)
            {
                if (_viewModel?.PrintVoucherCommand.CanExecute(null) == true)
                {
                    _viewModel.PrintVoucherCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }

    private void VoucherEntryView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.RequestFocusAdd -= ViewModel_RequestFocusAdd;
            _viewModel.RequestFocusFirstInput -= ViewModel_RequestFocusFirstInput;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as VoucherEntryViewModel;
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
        if (e.PropertyName == nameof(VoucherEntryViewModel.ToastMessage) ||
            e.PropertyName == nameof(VoucherEntryViewModel.ToastType))
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

    private void VoucherEntryView_Loaded(object sender, RoutedEventArgs e)
    {
        FocusAddNewButton();
        UpdateToastVisibility();
    }

    private void VoucherEntryView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
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
            DrAccountComboBox?.Focus();
            if (DrAccountComboBox != null)
            {
                Keyboard.Focus(DrAccountComboBox);
            }
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
