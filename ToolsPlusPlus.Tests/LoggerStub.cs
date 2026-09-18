using System.Diagnostics;
using System.Text;
using CompilePalX.Compiling;

namespace CompilePalX
{
    internal static class CompilePalLogger
    {
        private static readonly object Gate = new();
        public static readonly List<Error> Errors = [];
        public static readonly StringBuilder Output = new();

        public static void Reset()
        {
            lock (Gate)
            {
                Errors.Clear();
                Output.Clear();
            }
        }

        public static void LogLineDebug(string message) { }
        public static void LogDebug(string message) { }
        public static void LogLine(string message, int fontSize = 0) => LogProgressive(message + Environment.NewLine);
        public static void LogProgressive(string message)
        {
            lock (Gate)
                Output.Append(message);
        }
        public static void LogCompileError(string message, Error error)
        {
            lock (Gate)
                Errors.Add(error);
        }
    }

    internal class CompileMetadata
    {
        public string Name = "VBSP";
        public string Path = "";
        public string Arguments = "";
        public string? WorkingDirectory;
        public bool ReadOutput = true;
        public bool CheckExitCode = true;
    }

    internal class CompileContext
    {
        public string MapFile = "";
        public string CopyLocation = "";
        public GameConfiguration Configuration = new();
    }

    internal class CompileProcess(string metadata, string? parameterFolder = null)
    {
        public string? ParameterFolder = parameterFolder;
        public List<Error> CompileErrors = [];
        public CompileMetadata Metadata = new() { Name = metadata };
        public Process? Process;
        public string Name => Metadata.Name;
        public bool CanRun(CompileContext context) => true;
        public string GetParameterString() => Metadata.Arguments;
        public virtual void Run(CompileContext context, CancellationToken cancellationToken) { }
    }

    internal static class GameConfigurationManager
    {
        public static GameConfiguration GameConfiguration = new();
        public static string SubstituteValues(string text, string mapFile = "", bool quote = true)
        {
            string Format(string value) => quote ? $"\"{value}\"" : value;
            return text.Replace("$vmfFile$", Format(mapFile))
                .Replace("$binFolder$", Format(GameConfiguration.BinFolder))
                .Replace("$vbsp$", Format(GameConfiguration.VBSP));
        }
    }
}

namespace CompilePalX.Compiling
{
    internal enum ErrorSeverity { Warning = 3, FatalError = 5 }
    internal class Error(string description, ErrorSeverity severity)
    {
        public string Description = description;
        public ErrorSeverity Severity = severity;
    }
}
