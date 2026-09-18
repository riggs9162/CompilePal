using System;
using System.ComponentModel;
using System.Windows;

namespace CompilePalX;

/// <summary>
/// Interaction logic for GameConfigurationWindow.xaml
/// </summary>
public partial class GameConfigurationWindow
{
    private static GameConfigurationWindow? instance;
    public static GameConfigurationWindow Instance => instance ??= new GameConfigurationWindow();
    private int? index;

    private GameConfigurationWindow(GameConfiguration? gc = null)
    {
        InitializeComponent();
        gc ??= new GameConfiguration();
        this.DataContext = gc;
    }

    public void Open(GameConfiguration? gc = null, int? index = null, bool modal = false)
    {
        gc ??= new GameConfiguration();
        this.DataContext = gc;
        this.index = index;
        if (modal) ShowDialog();
        else { Show(); Focus(); }
    }


    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        var config = (GameConfiguration)this.DataContext;
        // TODO: validate
        // if index is not null, this is an edit
        if (this.index != null)
        {
            if (ReferenceEquals(GameConfigurationManager.GameConfiguration, GameConfigurationManager.GameConfigurations[(int)this.index]))
                GameConfigurationManager.GameConfiguration = config;
            GameConfigurationManager.GameConfigurations[(int)this.index] = config;
            AnalyticsManager.ModifyGameConfiguration(config.Name);
        }
        else
        {
            GameConfigurationManager.GameConfigurations.Add(config);
            AnalyticsManager.NewGameConfiguration(config.Name);
        }

        GameConfigurationManager.SaveGameConfigurations();
        LaunchWindow.Instance?.RefreshGameConfigurationList();
        MainWindow.Instance?.RefreshGameTools();
        Close();
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        instance = null;
        base.OnClosing(e);
    }
}
