using Autodesk.Revit.DB;

namespace FamVault.App.Services;

internal sealed class FamilyLoadOptions(bool overwrite) : IFamilyLoadOptions
{
    private readonly bool overwrite = overwrite;

    public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
    {
        overwriteParameterValues = overwrite;
        return overwrite;
    }

    public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
    {
        source = FamilySource.Family;
        overwriteParameterValues = overwrite;
        return overwrite;
    }
}
