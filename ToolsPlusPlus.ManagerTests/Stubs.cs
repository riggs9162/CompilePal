using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;

namespace CompilePalX.Annotations
{
    internal class NotifyPropertyChangedInvocatorAttribute : Attribute { }
}

namespace Newtonsoft.Json { internal class NamespaceMarker { } }
namespace CompilePalX.Compiling { internal class NamespaceMarker { } }

namespace CompilePalX
{
    public class TrulyObservableCollection<T> : ObservableCollection<T> { }
    public class Preset { public string Name { get; set; } = "Test"; }
    internal class CompileContext { public string CopyLocation { get; set; } = "fixture.bsp"; }
    internal class GameConfiguration { public string Name { get; set; } = "Test game"; }
    internal static class MainWindow { public static Dispatcher ActiveDispatcher { get; set; } }
    internal static class AnalyticsManager
    {
        public static void Compile() { }
        public static void CompileError() { }
    }
    internal static class ProgressManager
    {
        public static double Progress { get; set; }
        public static bool HasError { get; set; }
        public static void SetProgress(double progress) { Progress = progress; HasError = false; }
        public static void ErrorProgress() { HasError = true; }
    }
    internal static class GameConfigurationManager
    {
        private static GameConfiguration backup;
        public static GameConfiguration GameConfiguration { get; set; } = new();
        public static int Restores { get; set; }
        public static void BackupCurrentContext() { backup = GameConfiguration; }
        public static void RestoreCurrentContext() { GameConfiguration = backup; Restores++; }
        public static CompileContext BuildContext(Map map) => new();
    }
    internal static class OrderManager
    {
        public static List<Compilers.CompileProcess> CurrentOrder { get; set; } = [];
        public static void UpdateOrder() { }
    }
    internal class Error
    {
        public int ID { get; set; }
        public int Severity { get; set; }
        public string SeverityText => "Test error";
        public string ShortDescription => "Intentional test failure";
        public static Brush GetSeverityBrush(int severity) => Brushes.Red;
    }
    internal static class CompilePalLogger
    {
        public static event Action<Error> OnErrorFound;
        public static ConcurrentQueue<string> Lines { get; } = new();
        public static void LogLine(string value = "") => Lines.Enqueue(value);
        public static void Log(string value) => Lines.Enqueue(value);
        public static void LogDebug(string value) => Lines.Enqueue(value);
        public static void LogLineDebug(string value) => Lines.Enqueue(value);
        public static void LogLineFileLocation(string value, string path) => Lines.Enqueue(value);
        public static void LogLineColor(string value, Brush brush, params object[] args) => Lines.Enqueue(string.Format(value, args));
        public static void LogCompileError(string value, Error error)
        {
            Lines.Enqueue(value);
            OnErrorFound?.Invoke(error);
        }
    }
}

namespace CompilePalX.Compilers
{
    internal class Metadata { public bool DoRun { get; set; } = true; }
    internal class CompileProcess
    {
        public string Name { get; set; } = "TEST";
        public Metadata Metadata { get; } = new();
        public Dictionary<Preset, object> PresetDictionary { get; } = new();
        public List<Error> CompileErrors { get; } = [];
        public Action<CompileContext, CancellationToken> Handler { get; set; }
        public void Run(CompileContext context, CancellationToken token) => Handler(context, token);
    }
}

namespace CompilePalX.Configuration
{
    internal static class ConfigurationManager
    {
        public static Preset CurrentPreset { get; set; }
        public static List<Compilers.CompileProcess> CompileProcesses { get; set; } = [];
    }
}
