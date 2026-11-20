using System.Collections.ObjectModel;

namespace FamVault.App.UI.ViewModels;

public sealed class FamilyVersionGroupViewModel(int revitVersion) : ViewModelBase
{
    private bool isExpanded = true;

    public int RevitVersion { get; } = revitVersion;

    public ObservableCollection<FamilyGroupViewModel> Families { get; } = [];

    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }

    public string Title => $"Revit {RevitVersion}";

    public int FamilyCount => Families.Count;

    public int RevisionCount =>
        Families.Sum(family => family.RevisionCount);

    public long TotalSize =>
        Families.Sum(family => family.Revisions.Sum(item => item.FileSize));

    public string Summary =>
        $"{FamilyCount} families · {RevisionCount} revisions · " + $"{TotalSize / 1048576d:F2} MB";
}
