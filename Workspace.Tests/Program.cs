using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CompilePalX;
using CompilePalX.Compiling;
using Newtonsoft.Json;

internal static class Program
{
    private static int passed;
    private static string runtime = "";

    [STAThread]
    private static int Main(string[] args)
    {
        string originalDirectory = Environment.CurrentDirectory;
        runtime = Path.Combine(Path.GetTempPath(), "CompilePal-workspace-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runtime);
        try
        {
            string source = Path.GetFullPath(args.Length > 0 ? args[0] : "CompilePalX");
            CopyDirectory(Path.Combine(source, "Parameters"), Path.Combine(runtime, "Parameters"));
            File.Copy(Path.Combine(source, "version.txt"), Path.Combine(runtime, "version.txt"));
            File.Copy(Path.Combine(source, "version_prerelease.txt"), Path.Combine(runtime, "version_prerelease.txt"));
            Directory.CreateDirectory(Path.Combine(runtime, "Presets", "Workspace fixture"));
            var preset = new Preset { Name = "Workspace fixture", Processes = new()
            {
                ["VBSP"] = new(), ["VVIS"] = new(), ["VRAD"] = new(), ["PACK"] = new(), ["CUSTOM"] = new()
            }};
            File.WriteAllText(Path.Combine(runtime, "Presets", preset.Name, "meta.json"), JsonConvert.SerializeObject(preset));
            Directory.SetCurrentDirectory(runtime);
            var app = new App(true) { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            AppearanceManager.Initialize(Path.Combine(runtime, "workspace.json"));
            GameConfigurationManager.GameConfiguration = new GameConfiguration
            {
                Name = "Garry's Mod", SteamAppID = 4000, GameFolder = runtime, BinFolder = runtime,
                VBSP = Path.Combine(runtime, "vbsp++.exe"), VVIS = Path.Combine(runtime, "vvis++.exe"), VRAD = Path.Combine(runtime, "vrad++.exe"),
                BSPZip = Path.Combine(runtime, "bspzip++.exe"), MapFolder = runtime
            };
            var window = new MainWindow(false);
            string mapPath = Path.Combine(runtime, "workshop_atrium.vmf");
            File.WriteAllText(mapPath, "workspace test fixture");
            var map = new Map(mapPath, preset: ConfigurationManager.KnownPresets.Single());
            CompilingManager.MapFiles.Add(map);
            window.MapListBox.SelectedItem = map;
            var vrad = ConfigurationManager.CompileProcesses.Single(stage => stage.Name == "VRAD");
            window.CompileProcessesListBox.SelectedItem = vrad;
            Pump();
            Require(window.OptionCatalog.Items.Count > 90, "standalone argument catalog is visible");
            var options = window.OptionCatalog.Items.Cast<StageOption>().ToList();
            foreach (string stageName in new[] { "VBSP", "VVIS", "VRAD" })
            {
                var stage = ConfigurationManager.CompileProcesses.Single(process => process.Name == stageName);
                var catalog = JsonConvert.DeserializeObject<ConfigItem[]>(File.ReadAllText(Path.Combine(runtime, "Parameters", stageName, "toolsplusplus.json")))!;
                Require(catalog.All(option => stage.ParameterList.Any(item => string.Equals(item.Parameter?.Trim(), option.Parameter?.Trim(), StringComparison.OrdinalIgnoreCase))), stageName + " exposes every captured Tools++ help argument");
            }
            var ao = options.Single(option => option.Argument == "-ambientocclusion");
            ao.Enabled = true;
            Require(vrad.GetParameterString().Contains("-ambientocclusion"), "toggle writes the command");
            Require(vrad.ParameterList.Single(item => item.Parameter?.Trim() == "-ambientocclusion").Value == null, "catalog defaults are not changed");
            var radius = options.Single(option => option.Argument == "-aoradius");
            radius.Enabled = true;
            radius.Value = "64";
            Require(vrad.GetParameterString().Contains("-aoradius 64"), "value editor writes arguments");
            ao.Enabled = false;
            Require(!vrad.GetParameterString().Contains("-ambientocclusion"), "disabled options are removed");
            window.ParameterSearch.Text = "aoradius";
            Require(window.OptionCatalog.Items.Count == 1, "search matches raw flag");
            window.ParameterSearch.Text = "";
            Require(options.Single(option => option.Argument == "-game").CanToggle == false, "game path remains managed");
            var folder = options.Single(option => option.Argument == "-basedir");
            folder.Enabled = true;
            folder.Value = @"C:\Example content\Game";
            Require(vrad.GetParameterString().Contains("-basedir \"C:\\Example content\\Game\""), "folder argument values with spaces are quoted");
            folder.Enabled = false;
            GameConfigurationManager.GameConfiguration.VRAD = Path.Combine(runtime, "vrad.exe");
            window.RefreshGameTools();
            Require(!window.OptionCatalog.Items.Cast<StageOption>().Any(option => option.Argument == "-gpusubmit"), "stock tools hide unused standalone-only options");
            Require(window.OptionCatalog.Items.Cast<StageOption>().Any(option => option.Argument == "-aoradius" && option.Value == "64"), "tool switching preserves existing preset values");
            GameConfigurationManager.GameConfiguration.VRAD = Path.Combine(runtime, "vrad++.exe");
            window.RefreshGameTools();
            TestRepeatable();
            TestPreferences();
            var issues = WorkspacePreflight.Check(GameConfigurationManager.GameConfiguration, CompilingManager.MapFiles, ConfigurationManager.CompileProcesses);
            Require(issues.Any(issue => issue.Contains("executable not found")), "preflight reports missing tools");
            Require(WorkspacePreflight.Check(GameConfigurationManager.GameConfiguration, Array.Empty<Map>(), ConfigurationManager.CompileProcesses).Any(issue => issue.Contains("at least one map")), "preflight reports empty queue");
            CompilePalLogger.LogLine("Sample compile output");
            CompilePalLogger.LogLine("warning: sample warning");
            window.LogSearch.Text = "sample warning";
            Require(window.FilteredOutput.Text.Contains("sample warning") && !window.FilteredOutput.Text.Contains("Sample compile output"), "log search preserves matching lines");
            window.LogSearch.Text = "";
            window.IssuesOnly.IsChecked = true;
            Require(window.FilteredOutput.Text.Contains("sample warning"), "warning filter");
            window.IssuesOnly.IsChecked = false;
            Require(new TextRange(window.CompileOutputTextbox.Document.ContentStart, window.CompileOutputTextbox.Document.ContentEnd).Text.Contains("Sample compile output"), "filter does not destroy full log");
            string output = args.Length > 1 ? Path.GetFullPath(args[1], originalDirectory) : runtime;
            Directory.CreateDirectory(output);
            foreach (string mode in new[] { "Dark", "Light", "System" })
            {
                AppearanceManager.Preferences.Appearance = mode;
                AppearanceManager.Apply();
                window.ApplyWorkspacePreferences();
                Pump();
                var background = ((SolidColorBrush)app.FindResource("CompilePal.Brushes.DockBackground")).Color;
                Require(mode == "System" || (mode == "Dark" ? background.R < 40 : background.R > 240), mode + " theme applies");
                Render(window, Path.Combine(output, "workspace-" + mode.ToLowerInvariant() + ".png"), 1240, 820);
            }
            AppearanceManager.Preferences.Appearance = "Dark";
            AppearanceManager.Apply();
            var settings = new SettingsWindow();
            Render(settings, Path.Combine(output, "settings-dark.png"), 640, 660);
            Require(settings.FindName("AppearanceChoice") is ComboBox, "appearance is in settings");
            Require(window.FindName("AppearanceSelector") == null, "workspace has no direct theme selector");
            var custom = ConfigurationManager.CompileProcesses.Single(stage => stage.Name == "CUSTOM");
            window.CompileProcessesListBox.SelectedItem = custom;
            Require(window.ProcessTab.Visibility == Visibility.Visible && window.CatalogPanel.Visibility == Visibility.Collapsed, "custom programs and order remain accessible");
            window.CompileProcessesListBox.SelectedItem = vrad;
            Render(window, Path.Combine(output, "workspace-small.png"), 1000, 680);
            ConfigurationManager.SavePresets();
            ConfigurationManager.AssembleParameters();
            vrad = ConfigurationManager.CompileProcesses.Single(stage => stage.Name == "VRAD");
            var saved = vrad.PresetDictionary[ConfigurationManager.KnownPresets.Single()];
            Require(saved.Single(item => item.Parameter?.Trim() == "-aoradius").Value == "64", "new option survives preset save and reload");
            Require(!saved.Any(item => item.Parameter?.Trim() == "-ambientocclusion"), "disabled option stays absent after reload");
            Console.WriteLine($"{passed} workspace checks passed. Renderings: {output}");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { Directory.SetCurrentDirectory(originalDirectory); }
    }

    private static void TestRepeatable()
    {
        var active = new ObservableCollection<ConfigItem>();
        var template = new ConfigItem { Name = "Custom", CanHaveValue = true, CanBeUsedMoreThanOnce = true };
        var first = new StageOption(template, active, null, () => { });
        var second = new StageOption(template, active, null, () => { });
        first.Enabled = true;
        second.Enabled = true;
        first.Value = "one";
        second.Value = "two";
        first.Enabled = false;
        Require(active.Count == 1 && active[0].Value == "two", "repeated arguments remain independent");
        Require(template.Value == null, "editing values never changes catalog definitions");
    }

    private static void TestPreferences()
    {
        string path = Path.Combine(runtime, "preference-test.json");
        File.WriteAllText(path, "broken json");
        Require(WorkspacePreferences.Load(path).Appearance == "System", "corrupt preferences fall back safely");
        var prefs = new WorkspacePreferences { Appearance = "Dark", Compact = true, LogFontSize = 16, QueueWidth = double.NaN, Height = -1 };
        Require(prefs.Save(path), "preferences save");
        var loaded = WorkspacePreferences.Load(path);
        Require(loaded.Appearance == "Dark" && loaded.Compact && loaded.LogFontSize == 16, "preferences round trip");
        Require(loaded.QueueWidth == 245 && loaded.Height == 680, "invalid sizes are bounded");
    }

    private static void Render(Window window, string path, int width, int height)
    {
        window.Width = width;
        window.Height = height;
        var content = (FrameworkElement)window.Content;
        window.Content = null;
        content.SetValue(TextElement.ForegroundProperty, Application.Current.FindResource("MahApps.Brushes.Text"));
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        content.UpdateLayout();
        Pump();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var context = background.RenderOpen()) context.DrawRectangle((Brush)Application.Current.FindResource("CompilePal.Brushes.GridBackground"), null, new Rect(0, 0, width, height));
        bitmap.Render(background);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        window.Content = content;
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        Console.WriteLine("PASS " + name);
        passed++;
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

}

