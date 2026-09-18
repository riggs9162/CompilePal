using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CompilePalX.Configuration;

namespace CompilePalX
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class SettingsWindow
    {
        public SettingsWindow()
        {
            this.DataContext = ConfigurationManager.Settings.Clone();
            InitializeComponent();
            var preferences = AppearanceManager.Preferences;
            AppearanceChoice.SelectedValue = preferences.Appearance;
            CompactChoice.IsChecked = preferences.Compact;
            RememberChoice.IsChecked = preferences.RememberLayout;
            FollowChoice.IsChecked = preferences.FollowOutput;
            LogSizeChoice.Value = preferences.LogFontSize;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ConfigurationManager.SaveSettings((Settings) this.DataContext);
            var preferences = AppearanceManager.Preferences;
            preferences.Appearance = AppearanceChoice.SelectedValue?.ToString() ?? "System";
            preferences.Compact = CompactChoice.IsChecked == true;
            preferences.RememberLayout = RememberChoice.IsChecked == true;
            preferences.FollowOutput = FollowChoice.IsChecked == true;
            preferences.LogFontSize = LogSizeChoice.Value ?? 12;
            AppearanceManager.Save();
            AppearanceManager.Apply();
            MainWindow.Instance?.ApplyWorkspacePreferences();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private readonly Regex numberRegex = new Regex("[^0-9]+");
        private void ErrorCacheDurationDays_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = numberRegex.IsMatch(e.Text);
        }
    }
}
