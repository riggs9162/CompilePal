using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace CompilePalX;

public sealed class WorkspacePreferences
{
    public string Appearance { get; set; } = "System";
    public bool Compact { get; set; }
    public bool FollowOutput { get; set; } = true;
    public bool RememberLayout { get; set; } = true;
    public double LogFontSize { get; set; } = 12;
    public double QueueWidth { get; set; } = 245;
    public double OutputHeight { get; set; } = 210;
    public double MapRatio { get; set; } = 1;
    public double Width { get; set; } = 1240;
    public double Height { get; set; } = 820;

    public static WorkspacePreferences Load(string path)
    {
        try
        {
            var value = JsonSerializer.Deserialize<WorkspacePreferences>(File.ReadAllText(path)) ?? new();
            value.Normalize();
            return value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    public void Normalize()
    {
        if (Appearance is not ("System" or "Dark" or "Light")) Appearance = "System";
        QueueWidth = Bound(QueueWidth, 200, 400, 245);
        OutputHeight = Bound(OutputHeight, 110, 600, 210);
        MapRatio = Bound(MapRatio, 0.25, 4, 1);
        Width = Bound(Width, 1000, 3840, 1240);
        Height = Bound(Height, 680, 2160, 820);
        LogFontSize = Bound(LogFontSize, 10, 20, 12);
    }

    private static double Bound(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    public bool Save(string path)
    {
        try
        {
            Normalize();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(path + ".tmp", path, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

internal static class AppearanceManager
{
    private static string PreferencesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CompilePal++", "workspace.json");
    public static WorkspacePreferences Preferences { get; private set; } = new();
    private static bool initialized;

    public static void Initialize(string? preferencesPath = null)
    {
        if (initialized) return;
        initialized = true;
        if (preferencesPath != null) PreferencesPath = preferencesPath;
        Preferences = WorkspacePreferences.Load(PreferencesPath);
        Apply();
        SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
        Application.Current.Exit += (_, _) => SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;
    }

    private static void SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (Preferences.Appearance == "System" && !Application.Current.Dispatcher.HasShutdownStarted)
            Application.Current.Dispatcher.BeginInvoke(new Action(Apply));
    }

    public static void Save() => Preferences.Save(PreferencesPath);

    public static void Apply()
    {
        bool dark = Preferences.Appearance == "Dark";
        if (Preferences.Appearance == "System")
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                dark = key?.GetValue("AppsUseLightTheme") is int value && value == 0;
            }
            catch (System.Security.SecurityException) { dark = false; }
        }
        var resources = Application.Current.Resources;
        resources.MergedDictionaries[2] = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/MahApps.Metro;component/Styles/Themes/{(dark ? "Dark" : "Light")}.Pink.xaml")
        };
        Set("CompilePal.Brushes.GridBackground", dark ? "#15161A" : "#F2F3F6");
        Set("CompilePal.Brushes.DockBackground", dark ? "#1C1D22" : "#FFFFFF");
        Set("MahApps.Brushes.ThemeBackground", dark ? "#1C1D22" : "#FFFFFF");
        Set("MahApps.Brushes.ThemeForeground", dark ? "#ECECF2" : "#24242C");
        Set("MahApps.Brushes.Text", dark ? "#ECECF2" : "#24242C");
        Set("MahApps.Brushes.TextDisabled", dark ? "#9696A6" : "#686877");
        Set("CompilePal.Brushes.Link", dark ? "#FFA2D0" : "#A81260");
        Set("CompilePal.Brushes.CheckboxDisabled", dark ? "#494953" : "#CCCCD4");
        Set("MahApps.Brushes.WindowTitle", dark ? "#1C1D22" : "#FFFFFF");
        Set("MahApps.Brushes.WindowTitle.Foreground", dark ? "#ECECF2" : "#24242C");
        if (SystemParameters.HighContrast)
        {
            resources["CompilePal.Brushes.GridBackground"] = SystemColors.WindowBrush;
            resources["CompilePal.Brushes.DockBackground"] = SystemColors.WindowBrush;
            resources["MahApps.Brushes.Text"] = SystemColors.WindowTextBrush;
        }
    }

    private static void Set(string key, string color) => Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
}
