using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace CompilePalX;

public partial class GameConfigurationWindow
{
    private void SelectToolsPlusPlus_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not GameConfiguration config)
            return;

        var dialog = new OpenFolderDialog
        {
            Title = "Select the extracted Tools++ directory",
            Multiselect = false
        };
        string? initialDirectory = Path.GetDirectoryName(ToolsPlusPlusPaths.Clean(config.VBSP));
        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;
        if (dialog.ShowDialog(this) != true)
            return;

        if (!ToolsPlusPlusPaths.TrySelectSuite(dialog.FolderName, out var paths, out string error))
        {
            MessageBox.Show(this, error, "Tools++ configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        config.VBSP = paths["vbsp"];
        config.VVIS = paths["vvis"];
        config.VRAD = paths["vrad"];
        config.BSPZip = paths["bspzip"];

        DataContext = null;
        DataContext = config;
    }
}
