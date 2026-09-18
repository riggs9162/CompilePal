using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace CompilePalX;

/// <summary>A catalog option backed by the existing preset collection, without editing catalog defaults.</summary>
public sealed class StageOption : INotifyPropertyChanged
{
    private readonly ObservableCollection<ConfigItem> active;
    private readonly Action changed;
    private readonly ConfigItem definition;
    private ConfigItem? selected;
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Name => definition.Name;
    public string Argument => definition.Parameter?.Trim() ?? "";
    public string Description => definition.Description;
    public string Warning => definition.ToolsPlusPlusOnly ? "Tools++ only. " + definition.Warning : definition.Warning;
    public bool CanHaveValue => definition.CanHaveValue;
    public bool Repeatable => definition.CanBeUsedMoreThanOnce;
    public bool CanToggle => !string.Equals(Argument, "-game", StringComparison.OrdinalIgnoreCase);
    public bool IsCompatible => definition.IsCompatible;
    public ConfigItem Input => selected ?? definition;
    public string Value
    {
        get => Input.Value ?? "";
        set
        {
            if (selected == null) return;
            selected.Value = value;
            changed();
        }
    }
    public bool Enabled
    {
        get => selected != null;
        set
        {
            if (value == Enabled) return;
            if (value)
            {
                selected = (ConfigItem)definition.Clone();
                active.Add(selected);
            }
            else
            {
                active.Remove(selected!);
                selected = null;
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            changed();
        }
    }

    public StageOption(ConfigItem definition, ObservableCollection<ConfigItem> active, ConfigItem? selected, Action changed)
    {
        this.definition = definition;
        this.active = active;
        this.selected = selected;
        this.changed = changed;
    }

    public bool Matches(string query) => string.IsNullOrWhiteSpace(query) ||
        new[] { Name, Argument, Description, Warning }.Any(text => text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
}
