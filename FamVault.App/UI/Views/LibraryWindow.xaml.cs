using FamVault.App.Services;
using FamVault.App.UI.Models;
using FamVault.App.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace FamVault.App.UI.Views;

public partial class LibraryWindow : Window
{
    private readonly LibraryViewModel viewModel;
    private readonly IFamilyStorage storage;
    private bool isManagingLibrary;

    public LibraryWindow(IFamilyStorage storage)
    {
        InitializeComponent();

        this.storage = storage
            ?? throw new ArgumentNullException(nameof(storage));

        viewModel = new LibraryViewModel(this.storage);
        DataContext = viewModel;

        Loaded += LibraryWindow_Loaded;
        Closed += LibraryWindow_Closed;
    }

    private async void LibraryWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshLibraryAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (isManagingLibrary)
            return;

        await RefreshLibraryAsync();
    }

    private void CancelLoading_Click(object sender, RoutedEventArgs e)
    {
        viewModel.CancelLoading();
    }

    private void ExpandAll_Click(object sender, RoutedEventArgs e)
    {
        viewModel.ExpandAll();
    }

    private void CollapseAll_Click(object sender, RoutedEventArgs e)
    {
        viewModel.CollapseAll();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    public IReadOnlyList<FamilyLoadRequest> LoadRequests { get; private set; } = [];

    private void LoadSelected_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<FamilyLoadRequest> selected = viewModel.GetSelectedFamilies();

        if (selected.Count == 0)
        {
            MessageBox.Show(
                this,
                "Select at least one family.",
                "FamVault",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        LoadRequests = selected;
        DialogResult = true;
    }


    private void OpenActionsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        ContextMenu menu = button.ContextMenu;

        if (menu == null)
            return;

        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private async void DeleteFamily_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not FamilyGroupViewModel family)
            return;

        if (isManagingLibrary)
            return;

        MessageBoxResult confirmation = MessageBox.Show(
            this,
            $"Delete '{family.Name}' and all its revisions?\n\n" +
            "All Revit versions belonging to this family " +
            "will be removed from the active library.\n\n" +
            "The RFA files will be moved to FamVault Trash.",
            "FamVault - Delete Family",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirmation != MessageBoxResult.Yes)
            return;

        await ExecuteLibraryOperationAsync(
            () => storage.DeleteFamilyAsync(family.FamilyId, CancellationToken.None), "Delete Family");
    }

    private async void EditRevisionComment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.CommandParameter is not FamilyLibraryItem revision)
            return;

        EditRevisionCommentDialog dialog = new(revision.Comment)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
            return;

        await ExecuteLibraryOperationAsync(
                () => storage.UpdateRevisionCommentAsync(revision.FamilyId, revision.RevisionId, dialog.Comment, CancellationToken.None), "Edit Comment");
    }

    private async void DeleteRevision_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.CommandParameter is not FamilyLibraryItem revision)
            return;

        MessageBoxResult confirmation = MessageBox.Show(
            this,
            $"Delete revision {revision.RevisionNumber} of '{revision.Name}'?\n\n" +
            "The RFA file will be moved to FamVault Trash.\n" +
            "This operation will not affect families loaded in Revit.",
            "FamVault - Delete Revision",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirmation != MessageBoxResult.Yes)
            return;

        await ExecuteLibraryOperationAsync(
            () => storage.DeleteRevisionAsync(revision.FamilyId, revision.RevisionId, CancellationToken.None), "Delete Revision");

    }

    private async Task ExecuteLibraryOperationAsync(Func<Task> operation, string operationName)
    {
        if (isManagingLibrary)
            return;

        isManagingLibrary = true;

        try
        {
            IsEnabled = false;

            await operation();

            await viewModel.RefreshAsync();
            await LoadPreviewsAsync();
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
            IsEnabled = true;
            isManagingLibrary = false;
        }
    }

    private async Task RefreshLibraryAsync()
    {
        await viewModel.RefreshAsync();
        await LoadPreviewsAsync();
    }

    private async Task LoadPreviewsAsync()
    {
        foreach (FamilyVersionGroupViewModel versionGroup in viewModel.VersionGroups)
        {
            foreach (FamilyGroupViewModel family in versionGroup.Families)
            {
                foreach (FamilyLibraryItem revision in family.Revisions)
                {
                    if (viewModel.TryGetCachedPreview(revision.RevisionId, out ImageSource cachedPreview))
                    {
                        revision.Preview = cachedPreview;
                        continue;
                    }

                    try
                    {
                        byte[] bytes = await storage.GetPreviewAsync(revision.FamilyId, revision.RevisionId, CancellationToken.None);
                        ImageSource preview = FamilyPreviewImageService.FromBytes(bytes);

                        if (preview == null)
                            continue;

                        viewModel.CachePreview(revision.RevisionId, preview);

                        revision.Preview = preview;
                    }
                    catch (Exception exception)
                    {
                        System.Diagnostics.Debug.WriteLine($"Preview loading failed: {exception.Message}");
                    }
                }
            }
        }
    }

    private async void OpenTrash_Click(object sender, RoutedEventArgs e)
    {
        TrashWindow window = new(storage)
        {
            Owner = this
        };

        window.ShowDialog();

        if (window.HasChanges)
            await RefreshLibraryAsync();
    }

    private void LibraryWindow_Closed(object sender, EventArgs e)
    {
        viewModel.Dispose();
    }
}
