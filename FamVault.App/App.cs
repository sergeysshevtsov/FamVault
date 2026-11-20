using Autodesk.Revit.UI;
using FamVault.App.Services;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace FamVault.App;

public sealed class App : IExternalApplication
{
    private const string TabName = "SHSS Tools";
    private const string PanelName = "FamVault";
    private const string ResourceString = "pack://application:,,,/FamVault.App;component/Resources/Icons/";

    public Result OnStartup(UIControlledApplication application)
    {
#if NETFRAMEWORK
        AssemblyResolver.Register();
#endif
        string assemblyPath = Assembly.GetExecutingAssembly().Location;

        RibbonPanel panel = GetRibbonPanel(application, PanelName, TabName);

        PushButtonData libraryButtonData = new("FamVaultLibrary", "Family\nLibrary", assemblyPath, "FamVault.App.Commands.OpenLibraryCommand")
        {
            LargeImage = new BitmapImage(new Uri(string.Concat(ResourceString, "library32.png"))),
            Image = new BitmapImage(new Uri(string.Concat(ResourceString, "library16.png"))),
            ToolTip = "Browse, organize and load Revit families."
        };

        PushButtonData quickSaveButtonData = new("FamVaultQuickSave", "Quick\nSave", assemblyPath, "FamVault.App.Commands.QuickSaveCommand")
        {
            LargeImage = new BitmapImage(new Uri(string.Concat(ResourceString, "quickSave32.png"))),
            Image = new BitmapImage(new Uri(string.Concat(ResourceString, "quickSave16.png"))),
            ToolTip = "Save selected families to your default library."
        };
        PushButtonData saveSelectedButtonData = new("FamVaultSaveSelected", "Save with\nDetails", assemblyPath, "FamVault.App.Commands.SaveSelectedCommand")
        {
            LargeImage = new BitmapImage(new Uri(string.Concat(ResourceString, "saveDetails32.png"))),
            Image = new BitmapImage(new Uri(string.Concat(ResourceString, "saveDetails16.png"))),
            ToolTip = "Save selected Revit families with individual comments."
        };

        panel.AddItem(libraryButtonData);
        panel.AddItem(quickSaveButtonData);
        panel.AddItem(saveSelectedButtonData);

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application) =>
        Result.Succeeded;

    private RibbonPanel GetRibbonPanel(UIControlledApplication application, string ribbonName, string tabName = null)
    {
        IList<RibbonPanel> ribbonPanels;
        if (!string.IsNullOrEmpty(tabName))
        {
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch { }
            ribbonPanels = application.GetRibbonPanels(tabName);
        }
        else
            ribbonPanels = application.GetRibbonPanels();

        RibbonPanel ribbonPanel = null;
        foreach (RibbonPanel rp in ribbonPanels)
        {
            if (rp.Name.Equals(ribbonName))
            {
                ribbonPanel = rp;
                break;
            }
        }

        ribbonPanel ??= (string.IsNullOrEmpty(tabName)) ? application.CreateRibbonPanel(ribbonName) : application.CreateRibbonPanel(tabName, ribbonName);

        return ribbonPanel;
    }
}
