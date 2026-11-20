using FamVault.App.UI.Models;
using FamVault.Core.Models;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace FamVault.App.UI.ViewModels;

public sealed partial class LibraryViewModel(IFamilyStorage storage) : ViewModelBase, IDisposable
{
    private readonly IFamilyStorage storage = storage ??
            throw new ArgumentNullException(nameof(storage));
    private readonly List<FamilyLibraryItem> allItems = [];
    private CancellationTokenSource loadCancellation;
    private string searchText = string.Empty;
    private string statusText = "Ready";
    private bool isLoading;
    private string errorMessage = string.Empty;
    public ObservableCollection<FamilyVersionGroupViewModel> VersionGroups { get; } = [];
    private readonly Dictionary<(Guid FamilyId, int RevitVersion), Guid> selectedRevisions = [];
    private readonly Dictionary<Guid, ImageSource> previewCache = [];

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
                ApplyFilter();
        }
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public string ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (SetProperty(ref isLoading, value))
                OnPropertyChanged(nameof(IsNotLoading));
        }
    }

    public bool IsNotLoading =>
        !IsLoading;

    public async Task RefreshAsync()
    {
        if (IsLoading)
            return;

        CancellationTokenSource cancellation = new();

        loadCancellation = cancellation;
        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusText = "Loading families...";

        try
        {
            IReadOnlyList<FamilyEntry> families = await storage.GetFamiliesAsync(cancellation.Token);

            cancellation.Token.ThrowIfCancellationRequested();

            List<FamilyLibraryItem> loadedItems =
                [.. families.SelectMany(family =>
                    family.Revisions.Select(revision =>
                        new FamilyLibraryItem
                        {
                            FamilyId = family.Id,
                            RevisionId = revision.Id,
                            Name = family.Name,
                            Category = family.Category,
                            RevitVersion = revision.RevitVersion,
                            RevisionNumber = revision.RevisionNumber,
                            FileSize = revision.FileSize,
                            TypeCount = revision.TypeCount,
                            Comment = revision.Comment,
                            CreatedAtUtc = revision.CreatedAtUtc
                        }))];

            allItems.Clear();
            allItems.AddRange(loadedItems);

            ApplyFilter();
            RemoveInvalidSelections();
            RemoveInvalidCachedPreviews();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Loading cancelled";
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusText = "Failed to load library";
        }
        finally
        {
            IsLoading = false;
            loadCancellation = null;
            cancellation.Dispose();
        }
    }

    public void CancelLoading()
    {
        loadCancellation?.Cancel();
    }

    public void ExpandAll()
    {
        foreach (FamilyVersionGroupViewModel group in VersionGroups)
            group.IsExpanded = true;
    }

    public void CollapseAll()
    {
        foreach (FamilyVersionGroupViewModel group in VersionGroups)
            group.IsExpanded = false;
    }


    public void Dispose()
    {
        loadCancellation?.Cancel();
        previewCache.Clear();
    }
}
