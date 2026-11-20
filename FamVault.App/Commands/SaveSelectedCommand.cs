using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamVault.App.Services;
using FamVault.App.UI.ViewModels;
using FamVault.App.UI.Views;
using FamVault.Core.Models;
using FamVault.Storage.Local;
using System.IO;
using System.Windows.Interop;

namespace FamVault.App.Commands;

[Transaction(TransactionMode.Manual)]
public sealed class SaveSelectedCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;
            if (uiDocument == null)
                return Result.Cancelled;

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
                TaskDialog.Show("FamVault", "Select one or more loadable families first.");
                return Result.Cancelled;
            }

            SaveFamilyDialog dialog = new(families);
            WindowInteropHelper helper = new(dialog)
            {
                Owner = commandData.Application.MainWindowHandle
            };
            if (dialog.ShowDialog() != true)
                return Result.Cancelled;

            List<SaveFamilyItemViewModel> selected = [.. dialog.SelectedFamilies];
            IReadOnlyList<Family> selectedFamilies = [.. selected.Select(item => item.Family)];
            Dictionary<ElementId, string> comments = selected.ToDictionary(item => item.FamilyId, item => item.Comment);

            IReadOnlyList<string> results = service.SaveSelected(uiDocument, selectedFamilies, comments);

            TaskDialog.Show("FamVault - Save Results", string.Join(Environment.NewLine, results));

            return Result.Succeeded;
        }
        catch (Exception exception)
        {
            message = exception.ToString();
            return Result.Failed;
        }
    }
}
