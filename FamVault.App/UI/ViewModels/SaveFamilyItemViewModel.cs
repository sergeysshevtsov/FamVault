using Autodesk.Revit.DB;

namespace FamVault.App.UI.ViewModels;

public sealed class SaveFamilyItemViewModel(Family family) : ViewModelBase
{
    private bool isSelected = true;
    private string comment = string.Empty;

    public Family Family { get; } = family;

    public ElementId FamilyId => Family.Id;

    public string Name => Family.Name;

    public string Category =>
        Family.FamilyCategory?.Name ?? string.Empty;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public string Comment
    {
        get => comment;
        set => SetProperty(ref comment, value);
    }
}
