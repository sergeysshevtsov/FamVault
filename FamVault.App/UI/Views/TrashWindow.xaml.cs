using FamVault.App.UI.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using FamVault.App.UI.ViewModels;
using FamVault.App.Services;


namespace FamVault.App.UI.Views;

public partial class TrashWindow : Window
{
    private readonly IFamilyStorage storage;

    private readonly ObservableCollection<TrashGroupViewModel> groups = [];

    private List<TrashItemViewModel> allItems = [];

    private List<TrashGroupViewModel> allGroups = [];

    private bool isBusy;

    public bool HasChanges { get; private set; }

    public TrashWindow(IFamilyStorage storage)
    {
        InitializeComponent();

        this.storage = storage
            ?? throw new ArgumentNullException(nameof(storage));

        TrashItemsControl.ItemsSource = groups;

        Loaded += TrashWindow_Loaded;
    }

    private async void TrashWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (isBusy)
            return;

        isBusy = true;
        LoadingOverlay.Visibility = Visibility.Visible;

        try
        {
            IReadOnlyList<TrashEntry> entries = await storage.GetDeletedItemsAsync(CancellationToken.None);

            List<TrashItemViewModel> loadedItems = [.. entries.Select(entry => new TrashItemViewModel(entry))];

            foreach (TrashItemViewModel item in loadedItems)
            {
                try
                {
                    byte[] bytes = await storage.GetTrashPreviewAsync(item.Id, CancellationToken.None);
                    item.Preview = FamilyPreviewImageService.FromBytes(bytes);
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Debug.WriteLine($"Trash preview error: {exception}");
                }
            }

            allItems = loadedItems;
            allGroups = [.. allItems
                .GroupBy(item => (
                    GroupId: item.DeletionGroupId == Guid.Empty
                        ? item.Id
                        : item.DeletionGroupId,
                    FamilyId: item.Entry.FamilyId))
                .Select(group => new TrashGroupViewModel(
                    group.First().DeletionGroupId,
                    group.First().FamilyName,
                    group))
                .OrderBy(group => group.FamilyName)];

            ApplyFilter();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "FamVault - Trash",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            isBusy = false;
        }
    }

    private void ApplyFilter()
    {
        string search = SearchTextBox.Text.Trim();

        groups.Clear();

        foreach (TrashGroupViewModel group in allGroups)
        {
            bool matches = string.IsNullOrEmpty(search) || group.FamilyName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                group.Revisions.Any(item => item.RevisionDisplay.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);

            if (matches)
                groups.Add(group);
        }

        int revisionCount = groups.Sum(group => group.RevisionCount);
        StatusTextBlock.Text = $"{groups.Count} groups · {revisionCount} deleted revisions";
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RestoreAsync(Func<Task> restoreOperation)
    {
        if (isBusy)
            return;

        isBusy = true;
        LoadingOverlay.Visibility = Visibility.Visible;

        try
        {
            await restoreOperation();
            HasChanges = true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "FamVault - Restore",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            isBusy = false;
        }

        await RefreshAsync();
    }

    private async void RestoreRevision_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not TrashItemViewModel item)
            return;

        await RestoreAsync(() => storage.RestoreRevisionAsync(item.Id, CancellationToken.None));
    }

    private async void RestoreGroup_Click(object sender, RoutedEventArgs e)
    {
        if (isBusy || sender is not Button button || button.DataContext is not TrashGroupViewModel group || !group.CanRestoreAll)
            return;

        await RestoreAsync(() => storage.RestoreDeletionGroupAsync(group.DeletionGroupId, CancellationToken.None));
    }

    private async void DeletePermanently_Click(object sender, RoutedEventArgs e)
    {
        if (isBusy || sender is not Button button || button.DataContext is not TrashItemViewModel item)
            return;

        MessageBoxResult confirmation = MessageBox.Show(
            this,
            $"Permanently delete '{item.FamilyName}'?\n\n" +
            $"Revision: {item.RevisionDisplay}\n\n" +
            "The RFA and preview will be permanently deleted.\n" +
            "This action cannot be undone.",
            "FamVault - Permanent Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirmation != MessageBoxResult.Yes)
            return;

        await ExecuteTrashOperationAsync(() => storage.PermanentlyDeleteAsync(item.Id, CancellationToken.None), "Delete Permanently");
    }

    private async void EmptyTrash_Click(object sender, RoutedEventArgs e)
    {
        if (isBusy || allItems.Count == 0)
            return;

        MessageBoxResult confirmation = MessageBox.Show(
            this,
            $"Permanently delete {allItems.Count} " +
            "revisions from FamVault Trash?\n\n" +
            "This operation cannot be undone.",
            "FamVault - Empty Trash",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirmation != MessageBoxResult.Yes)
            return;

        await ExecuteTrashOperationAsync(() => storage.EmptyTrashAsync(CancellationToken.None), "Empty Trash");
    }

    private async Task ExecuteTrashOperationAsync(Func<Task> operation, string operationName)
    {
        if (isBusy)
            return;

        isBusy = true;
        LoadingOverlay.Visibility = Visibility.Visible;

        try
        {
            await operation();
            HasChanges = true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                $"FamVault - {operationName}",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            isBusy = false;
        }

        await RefreshAsync();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
