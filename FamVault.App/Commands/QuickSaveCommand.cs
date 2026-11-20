using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamVault.App.Services;
using FamVault.Core.Models;
using FamVault.Storage.Local;
using System.IO;

namespace FamVault.App.Commands;

[Transaction(TransactionMode.Manual)]
public sealed class QuickSaveCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;

            if (uiDocument == null)
            {
                TaskDialog.Show("FamVault", "Open a Revit project first.");
                return Result.Cancelled;
            }

            StorageLocation location = new()
            {
                Name = "Personal Library",
                ProviderId = "local",
                Location = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "FamVault"),
                IsDefault = true
            };

            IFamilyStorage storage = new LocalFamilyStorage(location);

            RevitFamilySaveService service = new(storage);

            IReadOnlyList<Family> families = service.GetSelectedFamilies(uiDocument);

            if (families.Count == 0)
            {
                TaskDialog.Show("FamVault", "Select one or more loadable Revit families.");
                return Result.Cancelled;
            }

            IReadOnlyList<string> results = service.SaveSelected(uiDocument);

            TaskDialog.Show("FamVault - Quick Save", string.Join(Environment.NewLine, results));

            return Result.Succeeded;
        }
        catch (Exception exception)
        {
            message = exception.ToString();
            TaskDialog.Show("FamVault - Quick Save Error", exception.ToString());
            return Result.Failed;
        }
    }
}
