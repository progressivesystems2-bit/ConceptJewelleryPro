using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public sealed class ItemGridLayoutModel
{
    public List<GridColumnLayoutModel> Columns { get; set; } = new();
}

public partial class ItemCreationView : UserControl
{
    private ItemCreationViewModel? _viewModel;
    private readonly ObservableCollection<ColumnChooserItem> _columnChooserItems = new();

    public ItemCreationView()
    {
        InitializeComponent();
        ColumnChooserList.ItemsSource = _columnChooserItems;
        Loaded += ItemCreationView_Loaded;
        DataContextChanged += ItemCreationView_DataContextChanged;
        AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnViewPreviewKeyDown), true);
    }

    private void OnViewPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.A)
            {
                if (_viewModel?.AddNewCommand.CanExecute(null) == true)
                {
                    _viewModel.AddNewCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (key == Key.X)
            {
                e.Handled = true;
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    SearchBox?.Focus();
                    SearchBox?.SelectAll();
                    if (SearchBox != null)
                    {
                        Keyboard.Focus(SearchBox);
                    }
                }));
            }
            else if (key == Key.M)
            {
                if (_viewModel?.ModifyCommand.CanExecute(null) == true)
                {
                    _viewModel.ModifyCommand.Execute(null);
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

    private void ItemCreationView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as ItemCreationViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemCreationViewModel.ToastMessage) ||
            e.PropertyName == nameof(ItemCreationViewModel.ToastType))
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

    private void ItemCreationView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateToastVisibility();
        LoadGridLayout();
        if (_viewModel != null && string.IsNullOrWhiteSpace(_viewModel.SearchText))
        {
            _viewModel.SelectedItem = null;
            ItemsGrid.SelectedItem = null;
        }
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            SearchBox.Focus();
        }));
    }

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel != null && _viewModel.SelectedItem != null)
        {
            if (_viewModel.ModifyCommand.CanExecute(null))
            {
                _viewModel.ModifyCommand.Execute(null);
            }
        }
    }

    private static readonly HashSet<string> DefaultVisibleHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Item Code", "Item Name", "Group", "Sub Group", "Metal Type",
        "Purity", "Standard Wt (g)", "MRP", "Sale Rate", "Opening Stock",
        "HSN Code", "GST Rate (%)", "Status"
    };

    private void ColumnChooserButton_DropDownOpened(object? sender, RoutedEventArgs e)
    {
        RefreshColumnChooserItems();
    }

    private void RefreshColumnChooserItems()
    {
        _columnChooserItems.Clear();
        if (ItemsGrid == null) return;

        foreach (var col in ItemsGrid.Columns.OfType<Telerik.Windows.Controls.GridViewColumn>())
        {
            var header = col.Header?.ToString();
            if (string.IsNullOrWhiteSpace(header)) continue;

            var item = new ColumnChooserItem
            {
                Header = header,
                Category = "Item Columns",
                Column = col
            };
            item.SetInitialVisibility(col.IsVisible);
            _columnChooserItems.Add(item);
        }
    }

    private void SelectAllColumns_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _columnChooserItems)
        {
            item.IsVisible = true;
        }
    }

    private void ResetColumns_Click(object sender, MouseButtonEventArgs e)
    {
        if (ItemsGrid == null) return;

        foreach (var col in ItemsGrid.Columns.OfType<Telerik.Windows.Controls.GridViewColumn>())
        {
            var header = col.Header?.ToString() ?? string.Empty;
            col.IsVisible = DefaultVisibleHeaders.Contains(header);
        }

        RefreshColumnChooserItems();

        try
        {
            var filePath = GetLayoutFilePath();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch { }

        ToastNotification.Message = "Reset to default columns";
        ToastNotification.ToastType = ToastType.Info;
        ToastNotification.Show();
    }

    private void SaveSettingsOption_Click(object sender, RoutedEventArgs e)
    {
        SaveGridLayout();
        ColumnChooserButton.IsOpen = false;
    }

    private static string GetLayoutFilePath()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ConceptJewelleryPro");
        if (!Directory.Exists(appData))
        {
            Directory.CreateDirectory(appData);
        }
        return Path.Combine(appData, "item_grid_layout.json");
    }

    public void SaveGridLayout()
    {
        try
        {
            if (ItemsGrid == null) return;
            var data = new ItemGridLayoutModel();
            foreach (Telerik.Windows.Controls.GridViewColumn col in ItemsGrid.Columns)
            {
                var header = col.Header?.ToString() ?? col.UniqueName ?? string.Empty;
                var width = col.ActualWidth > 0 ? col.ActualWidth : (col.Width.Value > 0 ? col.Width.Value : 100);
                data.Columns.Add(new GridColumnLayoutModel
                {
                    Header = header,
                    Width = width,
                    IsStar = col.Width.IsStar,
                    IsAuto = col.Width.IsAuto,
                    DisplayIndex = col.DisplayIndex,
                    IsVisible = col.IsVisible
                });
            }
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetLayoutFilePath(), json);

            ToastNotification.Message = "Item column layout saved successfully!";
            ToastNotification.ToastType = ToastType.Success;
            ToastNotification.Show();
        }
        catch (Exception ex)
        {
            ToastNotification.Message = "Error saving layout: " + ex.Message;
            ToastNotification.ToastType = ToastType.Error;
            ToastNotification.Show();
        }
    }

    public void LoadGridLayout()
    {
        try
        {
            var filePath = GetLayoutFilePath();
            if (!File.Exists(filePath) || ItemsGrid == null) return;

            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<ItemGridLayoutModel>(json);
            if (data?.Columns == null || data.Columns.Count == 0) return;

            foreach (Telerik.Windows.Controls.GridViewColumn col in ItemsGrid.Columns)
            {
                var header = col.Header?.ToString() ?? col.UniqueName ?? string.Empty;
                var saved = data.Columns.FirstOrDefault(c => string.Equals(c.Header, header, StringComparison.OrdinalIgnoreCase));
                if (saved != null)
                {
                    if (saved.Width > 0)
                    {
                        if (saved.IsStar)
                            col.Width = new Telerik.Windows.Controls.GridViewLength(saved.Width, Telerik.Windows.Controls.GridViewLengthUnitType.Star);
                        else if (saved.IsAuto)
                            col.Width = Telerik.Windows.Controls.GridViewLength.Auto;
                        else
                            col.Width = new Telerik.Windows.Controls.GridViewLength(saved.Width, Telerik.Windows.Controls.GridViewLengthUnitType.Pixel);
                    }
                    col.IsVisible = saved.IsVisible;
                }
            }

            var orderedSaved = data.Columns
                .Where(c => c.DisplayIndex >= 0 && c.DisplayIndex < ItemsGrid.Columns.Count)
                .OrderBy(c => c.DisplayIndex)
                .ToList();

            foreach (var saved in orderedSaved)
            {
                var col = ItemsGrid.Columns.OfType<Telerik.Windows.Controls.GridViewColumn>()
                    .FirstOrDefault(c => string.Equals(c.Header?.ToString() ?? c.UniqueName, saved.Header, StringComparison.OrdinalIgnoreCase));
                if (col != null)
                {
                    col.DisplayIndex = Math.Clamp(saved.DisplayIndex, 0, ItemsGrid.Columns.Count - 1);
                }
            }
        }
        catch
        {
            // Ignore errors on corrupted layout files
        }
    }
}
