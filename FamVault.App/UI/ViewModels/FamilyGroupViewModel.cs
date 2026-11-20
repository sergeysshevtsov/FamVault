using FamVault.App.UI.Models;
using System.Collections.ObjectModel;

namespace FamVault.App.UI.ViewModels;

public sealed class FamilyGroupViewModel(Guid familyId, string name, string category) : ViewModelBase
{
    private bool isExpanded;

    public Guid FamilyId { get; } = familyId;

    public string Name { get; } = name;

    public string Category { get; } = category;

    public ObservableCollection<FamilyLibraryItem> Revisions { get; } = [];

    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }

    public int RevisionCount => Revisions.Count;

    public FamilyLibraryItem LatestRevision =>
        Revisions.OrderByDescending(item => item.RevisionNumber).FirstOrDefault();

    public string SizeDisplay =>
        LatestRevision?.SizeDisplay ?? string.Empty;

    public string Comment =>
        LatestRevision?.Comment ?? string.Empty;

    private bool isSelected;
    private FamilyLibraryItem selectedRevision;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public FamilyLibraryItem SelectedRevision
    {
        get => selectedRevision;
        set => SetProperty(ref selectedRevision, value);
    }

    public void InitializeSelection()
    {
        SelectedRevision = LatestRevision;
    }
}
