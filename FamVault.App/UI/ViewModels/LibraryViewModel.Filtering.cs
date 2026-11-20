using FamVault.App.UI.Models;

namespace FamVault.App.UI.ViewModels;

public sealed partial class LibraryViewModel
{
    private void ApplyFilter()
    {
        CaptureSelection();

        Dictionary<int, bool> expandedVersions = VersionGroups.ToDictionary(group => group.RevitVersion, group => group.IsExpanded);

        Dictionary<(int Version, Guid FamilyId), bool> expandedFamilies =
            VersionGroups
                .SelectMany(version => version.Families.Select(family =>
                    new
                    {
                        version.RevitVersion,
                        family.FamilyId,
                        family.IsExpanded
                    }))
                .ToDictionary(
                    item => (item.RevitVersion, item.FamilyId),
                    item => item.IsExpanded);

        IEnumerable<FamilyLibraryItem> filteredItems = allItems;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string query = SearchText.Trim();

            filteredItems = filteredItems.Where(item =>
                item.Name.IndexOf(
                    query,
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                item.Category.IndexOf(
                    query,
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                item.Comment.IndexOf(
                    query,
                    StringComparison.OrdinalIgnoreCase) >= 0);
        }

        VersionGroups.Clear();

        foreach (IGrouping<int, FamilyLibraryItem> versionGroup in filteredItems.GroupBy(item => item.RevitVersion).OrderByDescending(group => group.Key))
        {
            FamilyVersionGroupViewModel versionViewModel = new(versionGroup.Key);
            if (expandedVersions.TryGetValue(versionGroup.Key, out bool isVersionExpanded))
                versionViewModel.IsExpanded = isVersionExpanded;

            foreach (IGrouping<Guid, FamilyLibraryItem> familyGroup in versionGroup.GroupBy(item => item.FamilyId).OrderBy(group => group.First().Name))
            {
                FamilyLibraryItem first = familyGroup.First();
                FamilyGroupViewModel familyViewModel = new(first.FamilyId, first.Name, first.Category);

                if (expandedFamilies.TryGetValue((versionGroup.Key, first.FamilyId), out bool isFamilyExpanded))
                    familyViewModel.IsExpanded = isFamilyExpanded;

                foreach (FamilyLibraryItem revision in familyGroup.OrderByDescending(item => item.RevisionNumber))
                    familyViewModel.Revisions.Add(revision);

                versionViewModel.Families.Add(familyViewModel);
                familyViewModel.InitializeSelection();

                (Guid FamilyId, int RevitVersion) key = (familyViewModel.FamilyId, versionGroup.Key);
                if (selectedRevisions.TryGetValue(key, out Guid revisionId))
                {
                    FamilyLibraryItem selectedRevision = familyViewModel.Revisions.FirstOrDefault(item => item.RevisionId == revisionId);
                    if (selectedRevision != null)
                    {
                        familyViewModel.SelectedRevision = selectedRevision;
                        familyViewModel.IsSelected = true;
                    }
                }
            }

            VersionGroups.Add(versionViewModel);
        }

        int familyCount = VersionGroups.Sum(group => group.FamilyCount);
        int revisionCount = VersionGroups.Sum(group => group.RevisionCount);

        StatusText = $"{familyCount} families · {revisionCount} revisions";
        RestoreCachedPreviews();
    }

    public IReadOnlyList<FamilyLoadRequest> GetSelectedFamilies()
    {
        CaptureSelection();

        List<FamilyLoadRequest> requests = [];

        foreach (KeyValuePair<(Guid FamilyId, int RevitVersion), Guid> selection in selectedRevisions)
        {
            FamilyLibraryItem? item = allItems.FirstOrDefault(family =>
                    family.FamilyId == selection.Key.FamilyId &&
                    family.RevitVersion == selection.Key.RevitVersion &&
                    family.RevisionId == selection.Value);

            if (item == null)
                continue;

            requests.Add(new FamilyLoadRequest
            {
                FamilyId = item.FamilyId,
                RevisionId = item.RevisionId,
                FamilyName = item.Name,
                RevitVersion = item.RevitVersion
            });
        }

        return requests;
    }

    private void CaptureSelection()
    {
        foreach (FamilyVersionGroupViewModel version in VersionGroups)
        {
            foreach (FamilyGroupViewModel family in version.Families)
            {
                (Guid FamilyId, int RevitVersion) key = (family.FamilyId, version.RevitVersion);

                if (!family.IsSelected || family.SelectedRevision == null)
                {
                    selectedRevisions.Remove(key);
                    continue;
                }

                selectedRevisions[key] = family.SelectedRevision.RevisionId;
            }
        }
    }

    private void RemoveInvalidSelections()
    {
        HashSet<Guid> availableRevisions = [.. allItems.Select(item => item.RevisionId)];

        List<(Guid FamilyId, int RevitVersion)> invalidKeys =
            [.. selectedRevisions
                .Where(item => !availableRevisions.Contains(item.Value))
                .Select(item => item.Key)];

        foreach ((Guid FamilyId, int RevitVersion) key in invalidKeys)
            selectedRevisions.Remove(key);
    }
}
