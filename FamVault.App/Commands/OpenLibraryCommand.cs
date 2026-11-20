using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamVault.App.Services;
using FamVault.App.UI.Views;
using FamVault.Core.Models;
using FamVault.Storage.Local;
using System.IO;
using System.Windows;
using System.Windows.Interop;

namespace FamVault.App.Commands;

[Transaction(TransactionMode.Manual)]
public sealed class OpenLibraryCommand : IExternalCommand
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
            LibraryWindow window = new(storage);

            WindowInteropHelper helper = new(window)
            {
                Owner = commandData.Application.MainWindowHandle
            };
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            bool? dialogResult = window.ShowDialog();

            if (dialogResult != true)
                return Result.Succeeded;

            IReadOnlyList<FamilyLoadRequest> requests = window.LoadRequests;
            if (requests.Count == 0)
                return Result.Cancelled;

            RevitFamilyLoadService service = new(storage);

            IReadOnlyList<string> results = service.LoadFamilies(uiDocument.Document, requests);
            int loadedCount = results.Count(item => item.EndsWith(": loaded", StringComparison.OrdinalIgnoreCase));

            TaskDialog.Show(
                "FamVault - Load Results",
                $"{loadedCount} of {requests.Count} families loaded."
                + Environment.NewLine
                + Environment.NewLine
                + string.Join(Environment.NewLine, results));

            return Result.Succeeded;
        }
        catch (Exception exception)
        {
            message = exception.ToString();
            return Result.Failed;
        }
    }
}
