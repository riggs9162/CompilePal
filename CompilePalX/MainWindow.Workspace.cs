using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace CompilePalX;

public partial class MainWindow
{
    private bool workspaceReady;
    private bool logDirty;
    private DispatcherTimer? logFilterTimer;
    private readonly List<string> issueMessages = new();
    private ObservableCollection<StageOption> stageOptions = new();

    private void InitializeWorkspace()
    {
        AppearanceManager.Initialize();
        var preferences = AppearanceManager.Preferences.RememberLayout ? AppearanceManager.Preferences : new WorkspacePreferences();
        Width = Math.Min(preferences.Width, Math.Max(MinWidth, SystemParameters.WorkArea.Width));
        Height = Math.Min(preferences.Height, Math.Max(MinHeight, SystemParameters.WorkArea.Height));
        QueueColumn.Width = new GridLength(preferences.QueueWidth);
        OutputRow.Height = new GridLength(Math.Min(preferences.OutputHeight, Height * .4));
        MapListBoxRow.Height = new GridLength(preferences.MapRatio, GridUnitType.Star);
        workspaceReady = true;
        ApplyWorkspacePreferences();
        RefreshQueueSummary();
        CompilingManager.MapFiles.CollectionChanged += (_, _) => RefreshQueueSummary();
        logFilterTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(350), DispatcherPriority.Background, (_, _) =>
        {
            if (logDirty && (IssuesOnly.IsChecked == true || !string.IsNullOrEmpty(LogSearch.Text))) RefreshLogFilter();
        }, Dispatcher);
        GameProfileText.Text = GameConfigurationManager.GameConfiguration?.Name ?? "Select a game";
        BuildOptionCatalog();
        CompilingManager.StageChanged += (process, status) => Dispatcher.Invoke(() =>
        {
            process.WorkspaceStatus = status;
            CompileStatusText.Text = $"{process.Name} · {status}";
        });
        CompilingManager.OutcomeChanged += status => Dispatcher.Invoke(() => CompileStatusText.Text = status);
    }

    private void SaveWorkspace()
    {
        if (!workspaceReady || !AppearanceManager.Preferences.RememberLayout) return;
        var preferences = AppearanceManager.Preferences;
        preferences.Width = WindowState == WindowState.Normal ? ActualWidth : RestoreBounds.Width;
        preferences.Height = WindowState == WindowState.Normal ? ActualHeight : RestoreBounds.Height;
        preferences.QueueWidth = QueueColumn.ActualWidth;
        preferences.OutputHeight = OutputRow.ActualHeight;
        double lowerHeight = Sidebar.RowDefinitions[2].ActualHeight;
        preferences.MapRatio = lowerHeight > 0 ? MapListBoxRow.ActualHeight / lowerHeight : 1;
        AppearanceManager.Save();
    }

    internal void ApplyWorkspacePreferences()
    {
        Resources["Workspace.OptionPadding"] = new Thickness(10, AppearanceManager.Preferences.Compact ? 5 : 11, 10, AppearanceManager.Preferences.Compact ? 5 : 11);
        ConfigDataGrid.RowHeight = AppearanceManager.Preferences.Compact ? 28 : 36;
        ProcessDataGrid.RowHeight = ConfigDataGrid.RowHeight;
        CompileOutputTextbox.FontSize = AppearanceManager.Preferences.LogFontSize;
        FilteredOutput.FontSize = AppearanceManager.Preferences.LogFontSize;
    }

    private void RefreshQueueSummary()
    {
        if (QueueSummary == null) return;
        int included = CompilingManager.MapFiles.Count(map => map.Compile);
        QueueSummary.Text = $"{included} of {CompilingManager.MapFiles.Count} maps included";
        CompileStartStopButton.IsEnabled = IsCompiling || included > 0;
    }

    private void BuildOptionCatalog()
    {
        if (OptionCatalog == null) return;
        CatalogPanel.Visibility = processModeEnabled ? Visibility.Collapsed : Visibility.Visible;
        ConfigDataGrid.Visibility = Visibility.Collapsed;
        ParameterSearch.IsEnabled = !processModeEnabled;
        AddParameterButton.Visibility = processModeEnabled ? Visibility.Visible : Visibility.Collapsed;
        RemoveParameterButton.Visibility = processModeEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (selectedProcess == null || ConfigurationManager.CurrentPreset == null || !selectedProcess.PresetDictionary.TryGetValue(ConfigurationManager.CurrentPreset, out var active))
        {
            OptionCatalog.ItemsSource = null;
            return;
        }
        var definitions = selectedProcess.ParameterList.ToList();
        var options = new List<StageOption>();
        var game = GameConfigurationManager.GameConfiguration;
        string? executable = selectedProcess.Name switch { "VBSP" => game?.VBSP, "VVIS" => game?.VVIS, "VRAD" => game?.VRAD, "REPACK" => game?.BSPZip, _ => null };
        bool standalone = ToolsPlusPlusPaths.IsPlusPlus(executable, selectedProcess.Name == "REPACK" ? "bspzip" : selectedProcess.Name.ToLowerInvariant());
        foreach (var definition in definitions)
        {
            var matches = active.Where(item => item.Name == definition.Name).ToList();
            if (definition.ToolsPlusPlusOnly && !standalone && matches.Count == 0) continue;
            if (matches.Count == 0) options.Add(new StageOption(definition, active, null, UpdateParameterTextBox));
            else options.AddRange(matches.Select(item => new StageOption(definition, active, item, UpdateParameterTextBox)));
        }
        foreach (var item in active.Where(item => !definitions.Any(definition => definition.Name == item.Name)))
            options.Add(new StageOption(item, active, item, UpdateParameterTextBox));
        stageOptions = new ObservableCollection<StageOption>(options.OrderByDescending(item => item.Enabled).ThenBy(item => item.Name));
        OptionCatalog.ItemsSource = new ListCollectionView(stageOptions);
        ApplyParameterFilter();
        OptionSummary.Text = $"{selectedProcess.Name} · {options.Count} options · {active.Count} enabled";
    }

    private void ParameterSearch_Changed(object sender, TextChangedEventArgs e) => ApplyParameterFilter();

    private void ApplyParameterFilter()
    {
        if (OptionCatalog?.ItemsSource is ICollectionView view)
            view.Filter = item => item is StageOption option && option.Matches(ParameterSearch.Text.Trim());
    }

    private void RepeatOption_Click(object sender, RoutedEventArgs e)
    {
        if (IsCompiling || (sender as FrameworkElement)?.DataContext is not StageOption option || selectedProcess == null || ConfigurationManager.CurrentPreset == null) return;
        selectedProcess.PresetDictionary[ConfigurationManager.CurrentPreset].Add((ConfigItem)option.Input.Clone());
        BuildOptionCatalog();
        UpdateParameterTextBox();
    }

    private void CopyCommand_Click(object sender, RoutedEventArgs e) => CopyText(ParametersTextBox.Text);

    private void CopyText(string text)
    {
        try { Clipboard.SetText(text ?? ""); }
        catch (System.Runtime.InteropServices.COMException) { CompileStatusText.Text = "Clipboard is busy. Try copying again."; }
    }

    private void Workspace_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        if (e.Key == Key.K) { ParameterSearch.Focus(); ParameterSearch.SelectAll(); e.Handled = true; }
        if (e.Key == Key.F) { LogSearch.Focus(); LogSearch.SelectAll(); e.Handled = true; }
    }

    private void LogSearch_Changed(object sender, TextChangedEventArgs e) => RefreshLogFilter();
    private void LogFilter_Changed(object sender, RoutedEventArgs e) => RefreshLogFilter();

    private void RefreshLogFilter()
    {
        if (FilteredOutput == null || CompileOutputTextbox == null) return;
        bool issues = IssuesOnly.IsChecked == true;
        string query = LogSearch.Text?.Trim() ?? "";
        bool filtered = issues || query.Length > 0;
        FilteredOutput.Visibility = filtered ? Visibility.Visible : Visibility.Collapsed;
        CompileOutputTextbox.Visibility = filtered ? Visibility.Collapsed : Visibility.Visible;
        if (filtered)
        {
            string text = new TextRange(CompileOutputTextbox.Document.ContentStart, CompileOutputTextbox.Document.ContentEnd).Text;
            var lines = text.Split('\n').Where(line => (!issues || IsIssueLine(line)) && (query.Length == 0 || line.Contains(query, StringComparison.OrdinalIgnoreCase)));
            FilteredOutput.Text = string.Join("\n", lines);
            if (FilteredOutput.Text.Length == 0) FilteredOutput.Text = "No matching output.";
        }
        logDirty = false;
    }

    private bool IsIssueLine(string line) => line.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("error", StringComparison.OrdinalIgnoreCase) || line.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
        issueMessages.Any(message => message.Split('\n').Any(part => part.Trim().Length > 0 && line.Contains(part.Trim(), StringComparison.OrdinalIgnoreCase)));

    private void GameTools_Click(object sender, RoutedEventArgs e)
    {
        if (IsCompiling) return;
        var game = GameConfigurationManager.GameConfiguration;
        int index = GameConfigurationManager.GameConfigurations.IndexOf(game);
        GameConfigurationWindow.Instance.Owner = this;
        GameConfigurationWindow.Instance.Open((GameConfiguration)game.Clone(), index >= 0 ? index : null, true);
    }

    internal void RefreshGameTools()
    {
        GameProfileText.Text = GameConfigurationManager.GameConfiguration?.Name;
        CompileProcessesListBox.Items.Refresh();
        BuildOptionCatalog();
    }

    private async void CheckTools_Click(object sender, RoutedEventArgs e)
    {
        var findings = WorkspacePreflight.Check(GameConfigurationManager.GameConfiguration, CompilingManager.MapFiles, ConfigurationManager.CompileProcesses);
        await ShowModal("Tool and queue checks", findings.Count == 0 ? "All enabled stages passed the path checks. This does not run compilers or validate map content." : string.Join("\n\n", findings));
    }
}
