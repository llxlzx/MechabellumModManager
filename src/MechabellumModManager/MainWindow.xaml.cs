using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using MechabellumModManager.Dialogs;
using MechabellumModManager.Helpers;
using MechabellumModManager.ViewModels;

namespace MechabellumModManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TryApplyBrandAssets();
        DataContextChanged += OnDataContextChanged;
    }

    void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        if (e.NewValue is INotifyPropertyChanged newVm)
            newVm.PropertyChanged += OnViewModelPropertyChanged;
    }

    void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ActiveContentPage))
            PlaySubtleFade(ContentPagesHost);
        if (e.PropertyName == nameof(MainViewModel.SortRevision) && sender is MainViewModel vm)
            SyncSortArrows(vm);
    }

    void SyncSortArrows(MainViewModel vm)
    {
        ApplyHeaderArrow(CatalogModsGrid, vm.ActiveCatalogSortPath, vm.ActiveCatalogSortDirection);
        ApplyHeaderArrow(LibraryModsGrid, vm.ActiveLibrarySortPath, vm.ActiveLibrarySortDirection);
    }

    static void ApplyHeaderArrow(DataGrid? grid, string path, ListSortDirection direction)
    {
        if (grid is null)
            return;
        foreach (var column in grid.Columns)
        {
            var columnPath = column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(columnPath) && column is DataGridBoundColumn { Binding: Binding binding })
                columnPath = binding.Path?.Path;
            column.SortDirection = string.Equals(columnPath, path, StringComparison.Ordinal)
                ? direction
                : null;
        }
    }

    static void PlaySubtleFade(UIElement? target)
    {
        if (target is null) return;
        var anim = new DoubleAnimation(0.78, 1.0, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        target.BeginAnimation(OpacityProperty, anim);
    }

    void LibraryModsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm && sender is DataGrid grid)
            vm.LibrarySelectionCount = grid.SelectedItems.Count;
    }

    /// <summary>
    /// Star columns only share the viewport when horizontal scrolling is off. Keep it off
    /// while the grid is wider than the column minimums, and turn it on when a narrow
    /// window would otherwise crush those minimums.
    /// </summary>
    void ModsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is DataGrid grid)
            FitDataGridColumns(grid);
    }

    static void FitDataGridColumns(DataGrid grid)
    {
        if (grid.ActualWidth <= 0 || grid.Columns.Count == 0)
            return;

        double fixedWidth = 0;
        double flexMin = 0;
        double weightSum = 0;
        foreach (var column in grid.Columns)
        {
            var min = column.MinWidth > 0 ? column.MinWidth : 0;
            var weight = GridColumnShare.GetWeight(column);
            if (weight > 0)
            {
                flexMin += min;
                weightSum += weight;
            }
            else
                fixedWidth += column.Width.IsAbsolute ? column.Width.Value : min;
        }

        var viewport = Math.Max(0, grid.ActualWidth - 18);
        var needsScroll = viewport < fixedWidth + flexMin;
        var next = needsScroll ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        if (ScrollViewer.GetHorizontalScrollBarVisibility(grid) != next)
            ScrollViewer.SetHorizontalScrollBarVisibility(grid, next);

        if (weightSum <= 0)
            return;

        var room = needsScroll ? flexMin : Math.Max(flexMin, viewport - fixedWidth);
        var leftover = Math.Max(0, room - flexMin);
        foreach (var column in grid.Columns)
        {
            var weight = GridColumnShare.GetWeight(column);
            if (weight <= 0)
                continue;
            var min = column.MinWidth > 0 ? column.MinWidth : 0;
            column.Width = new DataGridLength(min + leftover * (weight / weightSum));
        }
    }

    void ModsGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (sender is not DataGrid grid || DataContext is not MainViewModel vm || !e.Column.CanUserSort)
            return;

        var path = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(path) && e.Column is DataGridBoundColumn { Binding: Binding binding })
            path = binding.Path?.Path;
        if (string.IsNullOrWhiteSpace(path))
            return;

        var next = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        foreach (var column in grid.Columns)
            column.SortDirection = null;
        e.Column.SortDirection = next;
        vm.SortByColumn(ReferenceEquals(grid, CatalogModsGrid), path, next);
    }

    void CatalogModsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is not DataGrid grid) return;
        vm.SetCatalogSelection(grid.SelectedItems.Cast<CatalogModItemViewModel>().ToList());
    }

    void CatalogModCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not CheckBox) return;
        var row = FindVisualParent<DataGridRow>(sender as DependencyObject);
        if (row is null || CatalogModsGrid is null) return;

        e.Handled = true;

        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        if (shift && CatalogModsGrid.SelectedItems.Count > 0)
        {
            SelectCatalogRowRange(CatalogModsGrid, row, append: ctrl);
            return;
        }

        // Plain click and Ctrl both toggle — plain must NOT UnselectAll (multi-check via boxes).
        row.IsSelected = !row.IsSelected;
    }

    static void SelectCatalogRowRange(DataGrid grid, DataGridRow targetRow, bool append)
    {
        if (targetRow.DataContext is null) return;

        var items = grid.Items.Cast<object>().ToList();
        var anchor = grid.SelectedItem ?? grid.SelectedItems.Cast<object>().FirstOrDefault();
        var anchorIdx = anchor is not null ? items.IndexOf(anchor) : -1;
        var targetIdx = items.IndexOf(targetRow.DataContext);
        if (targetIdx < 0) return;
        if (anchorIdx < 0) anchorIdx = targetIdx;

        if (!append)
            grid.UnselectAll();

        var from = Math.Min(anchorIdx, targetIdx);
        var to = Math.Max(anchorIdx, targetIdx);
        for (var i = from; i <= to; i++)
        {
            var item = items[i];
            if (!grid.SelectedItems.Contains(item))
                grid.SelectedItems.Add(item);
        }
    }

    static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match) return match;
            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    internal void UnselectLibraryMods()
    {
        if (LibraryModsGrid != null)
            LibraryModsGrid.UnselectAll();
    }

    internal void UnselectCatalogMods()
    {
        if (CatalogModsGrid != null)
            CatalogModsGrid.UnselectAll();
    }

    void ModPreviewImage_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Image { Source: not null } image)
            return;
        e.Handled = true;
        var dialog = new PreviewImageDialog(image.Source)
        {
            Owner = this,
            Width = SystemParameters.WorkArea.Width * 0.85,
            Height = SystemParameters.WorkArea.Height * 0.85
        };
        dialog.ShowDialog();
    }

    void TryApplyBrandAssets()
    {
        try
        {
            var brandPath = Path.Combine(AppContext.BaseDirectory, "Assets", "brand.png");
            if (!File.Exists(brandPath))
                return;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(brandPath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            BrandImage.Source = bitmap;
            Icon = bitmap;
        }
        catch
        {
            // Brand art is optional; keep UI usable without it.
        }
    }
}
