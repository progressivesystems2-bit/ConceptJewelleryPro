using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class BarcodeCreationView : UserControl
{
    private BarcodeCreationViewModel? _viewModel;

    public BarcodeCreationView()
    {
        InitializeComponent();
        Loaded += BarcodeCreationView_Loaded;
        IsVisibleChanged += BarcodeCreationView_IsVisibleChanged;
        DataContextChanged += BarcodeCreationView_DataContextChanged;
    }

    private void BarcodeCreationView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.RequestFocusType -= ViewModel_RequestFocusType;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as BarcodeCreationViewModel;
        if (_viewModel != null)
        {
            _viewModel.RequestFocusType += ViewModel_RequestFocusType;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_RequestFocusType(object? sender, EventArgs e)
    {
        FocusTypePcWt();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BarcodeCreationViewModel.ToastMessage) ||
            e.PropertyName == nameof(BarcodeCreationViewModel.ToastType))
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

    private void BarcodeCreationView_Loaded(object sender, RoutedEventArgs e)
    {
        FocusAddNewButton();
        UpdateToastVisibility();
    }

    private void BarcodeCreationView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            FocusAddNewButton();
        }
    }

    private void AddNewButton_Click(object sender, RoutedEventArgs e)
    {
        FocusTypePcWt();
    }

    public void FocusAddNewButton()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            AddNewButton?.Focus();
        }));
    }

    public void FocusTypePcWt()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            TypePcWtComboBox?.Focus();
        }));
    }

    private void TypePcWtComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TypePcWtComboBox.SelectedItem is string sel && _viewModel != null)
        {
            _viewModel.TypePcWt = sel;
        }
    }

    private void StickerFormatComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Shift) == 0))
        {
            if (StickerFormatComboBox.IsDropDownOpen)
            {
                StickerFormatComboBox.IsDropDownOpen = false;
            }
            e.Handled = true;
            FocusSaveButton();
        }
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

    private void SearchBarcodeTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var text = SearchBarcodeTextBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                e.Handled = true;
                _viewModel?.SearchAndLoadBarcodeCommand.Execute(text);
            }
        }
    }
}
