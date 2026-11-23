using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ConceptJewelleryPro.Domain.Entities;
using Telerik.Windows.Controls;

namespace ConceptJewelleryPro.WPF.Helpers;

public static class ComboBoxNavigationHelper
{
    public static readonly DependencyProperty NavigatedItemProperty =
        DependencyProperty.RegisterAttached(
            "NavigatedItem",
            typeof(object),
            typeof(ComboBoxNavigationHelper),
            new PropertyMetadata(null));

    public static object? GetNavigatedItem(DependencyObject obj) => obj.GetValue(NavigatedItemProperty);
    public static void SetNavigatedItem(DependencyObject obj, object? value) => obj.SetValue(NavigatedItemProperty, value);

    private static readonly MethodInfo? RadNavigateToIndexMethod =
        typeof(RadComboBox).GetMethod("NavigateToIndex", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo? RadSetHighlightedElementMethod =
        typeof(RadComboBox).GetMethod("set_HighlightedElement", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly PropertyInfo? RadHighlightedElementProp =
        typeof(RadComboBox).GetProperty("HighlightedElement", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly PropertyInfo? RadHighlightedItemProp =
        typeof(RadComboBox).GetProperty("HighlightedItem", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo? KeyboardNavigationNavigateMethod =
        typeof(KeyboardNavigation).GetMethod(
            "Navigate",
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(DependencyObject), typeof(Key), typeof(ModifierKeys), typeof(bool) },
            null);

    private static readonly PropertyInfo? KeyboardNavigationCurrentProp =
        typeof(KeyboardNavigation).GetProperty(
            "Current",
            BindingFlags.NonPublic | BindingFlags.Static);

    public static bool HandleKeyDown(DependencyObject focusedElement, KeyEventArgs e)
    {
        if (e.Handled) return false;

        var radCombo = FindParent<RadComboBox>(focusedElement) ?? focusedElement as RadComboBox;
        if (radCombo != null)
        {
            return HandleRadComboBoxKeyDown(radCombo, focusedElement, e);
        }

        var stdCombo = FindParent<System.Windows.Controls.ComboBox>(focusedElement) ?? focusedElement as System.Windows.Controls.ComboBox;
        if (stdCombo != null)
        {
            return HandleStdComboBoxKeyDown(stdCombo, focusedElement, e);
        }

        return false;
    }

    private static bool HandleRadComboBoxKeyDown(RadComboBox combo, DependencyObject focusedElement, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up)
        {
            return HandleRadComboBoxArrowKey(combo, e);
        }

        if (e.Key is Key.Enter && (Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control | ModifierKeys.Alt)) == 0)
        {
            return HandleRadComboBoxEnterKey(combo, focusedElement, e);
        }

        return false;
    }

    private static bool HandleRadComboBoxArrowKey(RadComboBox combo, KeyEventArgs e)
    {
        if (!combo.IsDropDownOpen)
        {
            combo.IsDropDownOpen = true;
        }

        var visibleItems = GetFilteredVisibleItems(combo);
        if (visibleItems.Count == 0)
        {
            e.Handled = true;
            return true;
        }

        object? currentItem = GetNavigatedItem(combo) ?? GetRadHighlightedItem(combo);
        int currentIndex = -1;
        if (currentItem != null)
        {
            currentIndex = visibleItems.IndexOf(currentItem);
        }
        if (currentIndex < 0 && combo.SelectedItem != null)
        {
            currentIndex = visibleItems.IndexOf(combo.SelectedItem);
        }

        int nextIndex;
        if (e.Key == Key.Down)
        {
            nextIndex = (currentIndex >= 0 && currentIndex < visibleItems.Count - 1) ? currentIndex + 1 : 0;
        }
        else
        {
            nextIndex = (currentIndex > 0) ? currentIndex - 1 : visibleItems.Count - 1;
        }

        var targetItem = visibleItems[nextIndex];
        SetNavigatedItem(combo, targetItem);
        HighlightRadItem(combo, targetItem);

        e.Handled = true;
        return true;
    }

    private static bool HandleRadComboBoxEnterKey(RadComboBox combo, DependencyObject focusedElement, KeyEventArgs e)
    {
        object? itemToCommit = GetNavigatedItem(combo);

        if (itemToCommit == null && combo.IsDropDownOpen)
        {
            itemToCommit = GetRadHighlightedItem(combo);
        }

        string activeText = GetActiveText(combo, focusedElement);

        if (itemToCommit == null && !string.IsNullOrWhiteSpace(activeText))
        {
            var visibleItems = GetFilteredVisibleItems(combo);
            var candidates = visibleItems.Count > 0 ? visibleItems : combo.Items.Cast<object>().ToList();
            itemToCommit = FindBestMatch(candidates, activeText, item => GetItemText(combo, item));
        }

        if (itemToCommit == null && combo.SelectedItem != null)
        {
            itemToCommit = combo.SelectedItem;
        }

        if (itemToCommit != null)
        {
            combo.SelectedItem = itemToCommit;
            string committedText = GetItemText(combo, itemToCommit);
            if (!string.IsNullOrEmpty(committedText))
            {
                combo.Text = committedText;
                if (GetEditableTextBox(combo) is TextBox tb)
                {
                    tb.Text = committedText;
                    tb.SelectAll();
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(activeText))
        {
            combo.Text = activeText;
        }

        // Push values to TwoWay bindings if any
        combo.GetBindingExpression(RadComboBox.TextProperty)?.UpdateSource();
        combo.GetBindingExpression(RadComboBox.SelectedItemProperty)?.UpdateSource();

        if (combo.IsDropDownOpen)
        {
            combo.IsDropDownOpen = false;
        }
        SetNavigatedItem(combo, null);

        AdvanceFocus(combo, focusedElement);
        e.Handled = true;
        return true;
    }

    private static bool HandleStdComboBoxKeyDown(System.Windows.Controls.ComboBox combo, DependencyObject focusedElement, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up)
        {
            if (!combo.IsDropDownOpen)
            {
                combo.IsDropDownOpen = true;
            }

            var visibleItems = GetFilteredVisibleItems(combo);
            if (visibleItems.Count == 0)
            {
                e.Handled = true;
                return true;
            }

            object? currentItem = GetNavigatedItem(combo) ?? combo.SelectedItem;
            int currentIndex = currentItem != null ? visibleItems.IndexOf(currentItem) : -1;

            int nextIndex;
            if (e.Key == Key.Down)
            {
                nextIndex = (currentIndex >= 0 && currentIndex < visibleItems.Count - 1) ? currentIndex + 1 : 0;
            }
            else
            {
                nextIndex = (currentIndex > 0) ? currentIndex - 1 : visibleItems.Count - 1;
            }

            var targetItem = visibleItems[nextIndex];
            SetNavigatedItem(combo, targetItem);
            combo.SelectedItem = targetItem;
            ScrollItemIntoView(combo, targetItem);

            e.Handled = true;
            return true;
        }

        if (e.Key is Key.Enter && (Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control | ModifierKeys.Alt)) == 0)
        {
            object? itemToCommit = GetNavigatedItem(combo);

            string activeText = GetActiveText(combo, focusedElement);
            if (itemToCommit == null && !string.IsNullOrWhiteSpace(activeText))
            {
                var visibleItems = GetFilteredVisibleItems(combo);
                var candidates = visibleItems.Count > 0 ? visibleItems : combo.Items.Cast<object>().ToList();
                itemToCommit = FindBestMatch(candidates, activeText, item => GetItemText(combo, item));
            }

            if (itemToCommit == null && combo.SelectedItem != null)
            {
                itemToCommit = combo.SelectedItem;
            }

            if (itemToCommit != null)
            {
                combo.SelectedItem = itemToCommit;
                string committedText = GetItemText(combo, itemToCommit);
                if (!string.IsNullOrEmpty(committedText))
                {
                    combo.Text = committedText;
                    if (GetEditableTextBox(combo) is TextBox tb)
                    {
                        tb.Text = committedText;
                        tb.SelectAll();
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(activeText))
            {
                combo.Text = activeText;
            }

            // Push values to TwoWay bindings if any
            combo.GetBindingExpression(System.Windows.Controls.ComboBox.TextProperty)?.UpdateSource();
            combo.GetBindingExpression(System.Windows.Controls.ComboBox.SelectedItemProperty)?.UpdateSource();

            if (combo.IsDropDownOpen)
            {
                combo.IsDropDownOpen = false;
            }
            SetNavigatedItem(combo, null);

            AdvanceFocus(combo, focusedElement);
            e.Handled = true;
            return true;
        }

        return false;
    }

    public static List<object> GetFilteredVisibleItems(ItemsControl combo)
    {
        var list = new List<object>();
        if (combo.Items.Count == 0) return list;

        string filterText = string.Empty;
        bool isFiltering = false;
        TextSearchMode mode = TextSearchMode.Contains;

        if (combo is RadComboBox rad)
        {
            filterText = rad.Text ?? string.Empty;
            isFiltering = rad.IsFilteringEnabled;
            mode = rad.TextSearchMode;
        }
        else if (combo is System.Windows.Controls.ComboBox std)
        {
            filterText = std.Text ?? string.Empty;
            isFiltering = !string.IsNullOrWhiteSpace(filterText);
        }

        if (!isFiltering || string.IsNullOrWhiteSpace(filterText))
        {
            foreach (var item in combo.Items)
            {
                list.Add(item);
            }
            return list;
        }

        bool foundVisibleContainers = false;
        foreach (var item in combo.Items)
        {
            if (combo.ItemContainerGenerator.ContainerFromItem(item) is UIElement uie)
            {
                if (uie.Visibility == Visibility.Visible)
                {
                    list.Add(item);
                    foundVisibleContainers = true;
                }
            }
        }

        if (!foundVisibleContainers || list.Count == 0)
        {
            list.Clear();
            foreach (var item in combo.Items)
            {
                string itemText = GetItemText(combo, item);
                if (MatchesFilter(itemText, filterText, mode))
                {
                    list.Add(item);
                }
            }
        }

        return list;
    }

    private static bool MatchesFilter(string itemText, string filterText, TextSearchMode mode)
    {
        if (string.IsNullOrEmpty(filterText)) return true;
        if (string.IsNullOrEmpty(itemText)) return false;

        return mode switch
        {
            TextSearchMode.StartsWith => itemText.StartsWith(filterText, StringComparison.CurrentCultureIgnoreCase),
            _ => itemText.IndexOf(filterText, StringComparison.CurrentCultureIgnoreCase) >= 0
        };
    }

    public static object? FindBestMatch(IEnumerable<object> items, string query, Func<object, string> getText)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        query = query.Trim();

        object? exactMatch = null;
        object? prefixMatch = null;
        object? containsMatch = null;

        foreach (var item in items)
        {
            var text = getText(item);
            if (string.IsNullOrEmpty(text)) continue;

            if (string.Equals(text, query, StringComparison.CurrentCultureIgnoreCase))
            {
                exactMatch = item;
                break;
            }

            if (prefixMatch == null && text.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
            {
                prefixMatch = item;
            }
            else if (containsMatch == null && text.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                containsMatch = item;
            }
        }

        return exactMatch ?? prefixMatch ?? containsMatch;
    }

    public static string GetItemText(ItemsControl combo, object? item)
    {
        if (item == null) return string.Empty;
        if (item is string s) return s;
        if (item is RadComboBoxItem rcbi && rcbi.Content != null) return rcbi.Content.ToString() ?? string.Empty;
        if (item is System.Windows.Controls.ComboBoxItem cbi && cbi.Content != null) return cbi.Content.ToString() ?? string.Empty;

        string textPath = System.Windows.Controls.TextSearch.GetTextPath(combo);
        if (string.IsNullOrEmpty(textPath))
        {
            if (combo is RadComboBox rad)
            {
                textPath = rad.DisplayMemberPath;
            }
            else if (combo is System.Windows.Controls.ComboBox std)
            {
                textPath = std.DisplayMemberPath;
            }
        }

        if (!string.IsNullOrEmpty(textPath))
        {
            var prop = item.GetType().GetProperty(textPath, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
            {
                var val = prop.GetValue(item);
                return val?.ToString() ?? string.Empty;
            }
        }

        return item.ToString() ?? string.Empty;
    }

    private static void HighlightRadItem(RadComboBox combo, object targetItem)
    {
        int originalIndex = combo.Items.IndexOf(targetItem);
        if (originalIndex >= 0 && RadNavigateToIndexMethod != null)
        {
            try
            {
                RadNavigateToIndexMethod.Invoke(combo, new object[] { originalIndex });
            }
            catch {}
        }

        if (combo.ItemContainerGenerator.ContainerFromItem(targetItem) is FrameworkElement fe)
        {
            fe.BringIntoView();
            if (RadSetHighlightedElementMethod != null)
            {
                try
                {
                    RadSetHighlightedElementMethod.Invoke(combo, new object[] { fe });
                }
                catch {}
            }
        }
    }

    private static void ScrollItemIntoView(ItemsControl combo, object targetItem)
    {
        if (combo.ItemContainerGenerator.ContainerFromItem(targetItem) is FrameworkElement fe)
        {
            fe.BringIntoView();
        }
    }

    public static object? GetRadHighlightedItem(RadComboBox combo)
    {
        try
        {
            if (RadHighlightedItemProp != null)
            {
                var item = RadHighlightedItemProp.GetValue(combo);
                if (item != null) return item;
            }

            if (RadHighlightedElementProp != null)
            {
                var elem = RadHighlightedElementProp.GetValue(combo);
                if (elem is FrameworkElement fe)
                {
                    var item = combo.ItemContainerGenerator.ItemFromContainer(fe);
                    if (item != null && item != DependencyProperty.UnsetValue) return item;
                    if (fe is RadComboBoxItem rcbi && rcbi.Content != null) return rcbi.Content;
                    if (fe.DataContext != null) return fe.DataContext;
                }
            }
        }
        catch {}

        return null;
    }

    private static string GetActiveText(ItemsControl combo, DependencyObject focusedElement)
    {
        if (focusedElement is TextBox tb && !string.IsNullOrWhiteSpace(tb.Text))
        {
            return tb.Text.Trim();
        }

        if (GetEditableTextBox(combo) is TextBox partTb && !string.IsNullOrWhiteSpace(partTb.Text))
        {
            return partTb.Text.Trim();
        }

        if (combo is RadComboBox rad && !string.IsNullOrWhiteSpace(rad.Text))
        {
            return rad.Text.Trim();
        }

        if (combo is System.Windows.Controls.ComboBox std && !string.IsNullOrWhiteSpace(std.Text))
        {
            return std.Text.Trim();
        }

        return string.Empty;
    }

    private static TextBox? GetEditableTextBox(ItemsControl? combo)
    {
        if (combo == null) return null;
        combo.ApplyTemplate();
        var part = combo.Template?.FindName("PART_EditableTextBox", combo);
        if (part is TextBox tb) return tb;
        return FindVisualChild<TextBox>(combo);
    }

    private static void AdvanceFocus(UIElement comboOrContainer, DependencyObject? focusedElement = null)
    {
        void MoveNext()
        {
            var target = (focusedElement as UIElement)
                         ?? (Keyboard.FocusedElement as UIElement)
                         ?? (comboOrContainer is ItemsControl ic ? GetEditableTextBox(ic) as UIElement : null)
                         ?? comboOrContainer;

            bool navigated = false;

            // 1. Primary: Use WPF's internal KeyboardNavigation.Current.Navigate(target, Key.Tab, ModifierKeys.None, fromProcessInputTabKey: true)
            // This is the exact mechanism WPF uses when the user presses the Tab key, which seamlessly crosses container boundaries,
            // respects TabIndex, skips non-tabstops, and navigates to the next control.
            try
            {
                if (KeyboardNavigationCurrentProp != null && KeyboardNavigationNavigateMethod != null)
                {
                    var currentKn = KeyboardNavigationCurrentProp.GetValue(null);
                    if (currentKn != null)
                    {
                        var result = KeyboardNavigationNavigateMethod.Invoke(
                            currentKn,
                            new object[] { target, Key.Tab, ModifierKeys.None, true });
                        if (result is bool b && b)
                        {
                            navigated = true;
                        }
                    }
                }
            }
            catch
            {
                navigated = false;
            }

            // 2. Fallback: Temporarily set KeyboardNavigation.TabNavigation to Continue on the container
            // so standard UIElement.MoveFocus escapes the Local container rather than re-focusing its internal TextBox.
            if (!navigated)
            {
                var container = (comboOrContainer as FrameworkElement) ?? FindParent<FrameworkElement>(target);
                KeyboardNavigationMode? origMode = null;
                if (container != null)
                {
                    origMode = KeyboardNavigation.GetTabNavigation(container);
                    KeyboardNavigation.SetTabNavigation(container, KeyboardNavigationMode.Continue);
                }

                try
                {
                    var request = new TraversalRequest(FocusNavigationDirection.Next);
                    if (!target.MoveFocus(request) && container != null)
                    {
                        container.MoveFocus(request);
                    }
                }
                finally
                {
                    if (container != null && origMode.HasValue)
                    {
                        KeyboardNavigation.SetTabNavigation(container, origMode.Value);
                    }
                }
            }
        }

        // Execute immediately
        MoveNext();

        // Also schedule a check on DispatcherPriority.Input. If closing a dropdown popup
        // or a binding update somehow reclaimed focus back into this combo, push focus forward again!
        comboOrContainer.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
        {
            if (comboOrContainer is UIElement ui && ui.IsKeyboardFocusWithin)
            {
                MoveNext();
            }
        }));
    }

    private static T? FindParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match) return match;

            if (current is FrameworkElement fe && fe.Parent is T parentMatch)
                return parentMatch;

            if (current is RadComboBoxItem rcbi && typeof(T).IsAssignableFrom(typeof(RadComboBox)))
            {
                var owner = ItemsControl.ItemsControlFromItemContainer(rcbi) as T;
                if (owner != null) return owner;
            }

            if (current is System.Windows.Controls.ComboBoxItem cbi && typeof(T).IsAssignableFrom(typeof(System.Windows.Controls.ComboBox)))
            {
                var owner = ItemsControl.ItemsControlFromItemContainer(cbi) as T;
                if (owner != null) return owner;
            }

            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;
            var nested = FindVisualChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }
}

public class PrefixPriorityFilteringBehavior : Telerik.Windows.Controls.ComboBoxFilteringBehavior
{
    public override System.Collections.Generic.List<int> FindMatchingIndexes(string text)
    {
        if (ComboBox == null || ComboBox.Items.Count == 0)
            return new System.Collections.Generic.List<int>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return new System.Collections.Generic.List<int>();
        }

        var trimmed = text.Trim();
        var words = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var exactMatches = new System.Collections.Generic.List<int>();
        var prefixMatches = new System.Collections.Generic.List<int>();
        var containsMatches = new System.Collections.Generic.List<int>();
        var allWordsMatches = new System.Collections.Generic.List<int>();

        for (int i = 0; i < ComboBox.Items.Count; i++)
        {
            var item = ComboBox.Items[i];
            var str = (item as string) ?? item?.ToString();
            if (string.IsNullOrWhiteSpace(str)) continue;
            str = str.Trim();

            if (string.Equals(str, trimmed, StringComparison.CurrentCultureIgnoreCase))
            {
                exactMatches.Add(i);
            }
            else if (str.StartsWith(trimmed, StringComparison.CurrentCultureIgnoreCase))
            {
                prefixMatches.Add(i);
            }
            else if (str.IndexOf(trimmed, StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                containsMatches.Add(i);
            }
            else if (words.Length > 1 && words.All(w => str.IndexOf(w, StringComparison.CurrentCultureIgnoreCase) >= 0))
            {
                allWordsMatches.Add(i);
            }
        }

        var result = new System.Collections.Generic.List<int>(exactMatches.Count + prefixMatches.Count + containsMatches.Count + allWordsMatches.Count);
        result.AddRange(exactMatches);
        result.AddRange(prefixMatches);
        result.AddRange(containsMatches);
        result.AddRange(allWordsMatches);
        return result;
    }

    public override int FindFullMatchIndex(System.Collections.ObjectModel.ReadOnlyCollection<int> matchIndexes)
    {
        var text = ComboBox?.Text?.Trim();
        if (string.IsNullOrEmpty(text) || matchIndexes == null || matchIndexes.Count == 0)
            return -1;

        for (int i = 0; i < matchIndexes.Count; i++)
        {
            int idx = matchIndexes[i];
            if (idx >= 0 && idx < ComboBox.Items.Count)
            {
                var item = ComboBox.Items[idx];
                string? val = (item as string) ?? item?.ToString();
                if (string.Equals(val?.Trim(), text, StringComparison.OrdinalIgnoreCase))
                    return idx;
            }
        }
        return -1;
    }
}

public class BarcodeStartsWithFilteringBehavior : Telerik.Windows.Controls.ComboBoxFilteringBehavior
{
    public override System.Collections.Generic.List<int> FindMatchingIndexes(string text)
    {
        if (ComboBox == null || ComboBox.Items.Count == 0)
            return new System.Collections.Generic.List<int>();

        if (string.IsNullOrWhiteSpace(text))
        {
            var all = new System.Collections.Generic.List<int>(ComboBox.Items.Count);
            for (int i = 0; i < ComboBox.Items.Count; i++)
                all.Add(i);
            return all;
        }

        var trimmed = text.Trim();
        var matches = new System.Collections.Generic.List<int>();

        for (int i = 0; i < ComboBox.Items.Count; i++)
        {
            var item = ComboBox.Items[i];
            string? barcode = null;
            if (item is TempStockItem stockItem)
            {
                barcode = stockItem.Barcode;
            }
            else if (item is string s)
            {
                barcode = s;
            }
            else
            {
                barcode = item?.ToString();
            }

            if (string.IsNullOrWhiteSpace(barcode)) continue;

            // Only match from beginning digits / characters (StartsWith)
            if (barcode.Trim().StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(i);
            }
        }

        return matches;
    }

    public override int FindFullMatchIndex(System.Collections.ObjectModel.ReadOnlyCollection<int> matchIndexes)
    {
        var text = ComboBox?.Text?.Trim();
        if (string.IsNullOrEmpty(text) || matchIndexes == null || matchIndexes.Count == 0)
            return -1;

        for (int i = 0; i < matchIndexes.Count; i++)
        {
            int idx = matchIndexes[i];
            if (idx >= 0 && idx < ComboBox.Items.Count)
            {
                var item = ComboBox.Items[idx];
                string? val = (item as TempStockItem)?.Barcode ?? (item as string) ?? item?.ToString();
                if (string.Equals(val?.Trim(), text, StringComparison.OrdinalIgnoreCase))
                    return idx;
            }
        }
        return -1;
    }
}

public sealed class PrefixPriorityComparer : System.Collections.IComparer
{
    private readonly string _query;

    public PrefixPriorityComparer(string query)
    {
        _query = query?.Trim() ?? string.Empty;
    }

    public int Compare(object? x, object? y)
    {
        string sx = (x as string) ?? x?.ToString() ?? string.Empty;
        string sy = (y as string) ?? y?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(_query))
        {
            return string.Compare(sx, sy, StringComparison.OrdinalIgnoreCase);
        }

        bool xStarts = sx.StartsWith(_query, StringComparison.OrdinalIgnoreCase);
        bool yStarts = sy.StartsWith(_query, StringComparison.OrdinalIgnoreCase);

        // If one starts with query and the other doesn't, prefix match comes first
        if (xStarts && !yStarts) return -1;
        if (!xStarts && yStarts) return 1;

        // Otherwise alphabetical order
        return string.Compare(sx, sy, StringComparison.OrdinalIgnoreCase);
    }
}





