using CompilePalX.Compiling;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CompilePalX.Compilers
{
    /// <summary>Builds cubemaps with SourceCubemap, which renders them on the graphics card without starting the game.</summary>
    class SourceCubemapProcess : CompileProcess
    {
        private const int GarrysModAppId = 4000;
        private const string ErrorPrefix = "Error: ";
        private static readonly TimeSpan CancelGrace = TimeSpan.FromSeconds(5);
        private static readonly Regex StrictOption = new(@"(?:^|\s)-strict(?=\s|$)");

        public SourceCubemapProcess() : base("SOURCECUBEMAP") { }

        public override void Run(CompileContext context, CancellationToken cancellationToken)
        {
            CompileErrors = [];
            if (!CanRun(context)) return;

            string report = Path.Combine(Path.GetTempPath(), $"sourcecubemap-{Guid.NewGuid():N}.json");
            try
            {
                CompilePalLogger.LogLine("\nCompilePal++ - SourceCubemap", 900);
                string executable = ToolsPlusPlusPaths.Resolve(context.Configuration.SourceCubemap, ".");
                if (!File.Exists(executable))
                    throw new FileNotFoundException("SOURCECUBEMAP needs sourcecubemap.exe. Set its path in Game & tools.", executable);
                string bspFile = ToolsPlusPlusPaths.Resolve(context.CopyLocation, ".");
                if (!File.Exists(bspFile))
                    throw new FileNotFoundException("Could not find the BSP for SOURCECUBEMAP.", bspFile);

                RunTool(executable, BuildArguments(context, bspFile, report), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                ReportResult(report);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                CompilePalLogger.LogDebug(exception.ToString());
                CompilePalLogger.LogCompileError($"SOURCECUBEMAP failed: {exception.Message}\n",
                    new Error($"SOURCECUBEMAP failed: {exception.Message}", ErrorSeverity.FatalError));
            }
            finally
            {
                File.Delete(report);
            }
        }

        /// <summary>
        /// Keeps the map's backup only for a queued BSP, because a compiled map can be rebuilt from its source.
        /// Skips the cache because every compile changes the map, so entries would only fill the disk.
        /// </summary>
        private string BuildArguments(CompileContext context, string bspFile, string report)
        {
            string gameFolder = Path.TrimEndingDirectorySeparator(ToolsPlusPlusPaths.Resolve(context.Configuration.GameFolder, "."));
            string parameters = GetParameterString();
            string options = StrictOption.Replace(parameters, " ").Trim();
            string arguments = $"build \"{bspFile}\" --game \"{gameFolder}\" --in-place --no-cache --progress-json --json \"{report}\"";
            if (!context.Map.IsBSP)
                arguments += " --no-backup";
            if (!Regex.IsMatch(options, @"(?:^|\s)--profile(?=\s|$)"))
                arguments += $" --profile {ProfileFor(context.Configuration, gameFolder)}";
            if (!StrictOption.IsMatch(parameters))
                arguments += " --experimental --water planar";
            return $"{arguments} {options}".TrimEnd();
        }

        private static string ProfileFor(GameConfiguration game, string gameFolder)
        {
            bool garrysMod = game.SteamAppID == GarrysModAppId
                || string.Equals(Path.GetFileName(gameFolder), "garrysmod", StringComparison.OrdinalIgnoreCase);
            return garrysMod ? "gmod" : "hl2";
        }

        /// <summary>Lets SourceCubemap stop at a safe point when cancelled, and only ends it by force if it does not.</summary>
        private void RunTool(string executable, string arguments, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string cancelEventName = $"CompilePalSourceCubemap-{Guid.NewGuid():N}";
            using var cancelEvent = new EventWaitHandle(false, EventResetMode.ManualReset, cancelEventName);
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(executable, $"{arguments} --cancel-event {cancelEventName}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(executable) ?? ".",
                }
            };
            CompilePalLogger.LogLineDebug($"Running '{executable}' with args '{process.StartInfo.Arguments}'");
            Process = process;
            try
            {
                process.Start();
                using var registration = cancellationToken.Register(() =>
                {
                    cancelEvent.Set();
                    Task.Delay(CancelGrace).ContinueWith(_ => StopTool(process));
                });
                Task<string> progress = process.StandardOutput.ReadToEndAsync();
                string failure = ReadLog(process.StandardError);
                progress.GetAwaiter().GetResult();
                process.WaitForExit();
                cancellationToken.ThrowIfCancellationRequested();
                if (process.ExitCode != 0)
                    throw new IOException(failure.Length > 0 ? failure : $"SourceCubemap exited with code {process.ExitCode}.");
            }
            finally
            {
                Process = null;
            }
        }

        /// <summary>Logs every line and returns the last error SourceCubemap printed.</summary>
        private static string ReadLog(StreamReader log)
        {
            string failure = string.Empty;
            while (log.ReadLine() is { } line)
            {
                if (line.StartsWith(ErrorPrefix, StringComparison.Ordinal))
                    failure = line[ErrorPrefix.Length..];
                else
                    CompilePalLogger.LogLine(line);
            }
            return failure;
        }

        private static void ReportResult(string report)
        {
            JObject result = JObject.Parse(File.ReadAllText(report));
            int cubemaps = result.Value<int>("probes");
            double seconds = result["timings_seconds"]?.Value<double>("total") ?? 0;
            CompilePalLogger.LogLine($"Cubemaps compiled: {cubemaps} in {seconds:0.0} s");

            int missing = result["scenes"]?.Max(scene => scene["geometry"]?.Value<int>("missing_assets") ?? 0) ?? 0;
            if (missing > 0)
            {
                string warning = $"SourceCubemap replaced or left out {missing} missing assets, so reflections will differ.";
                CompilePalLogger.LogCompileError($"{warning}\n", new Error(warning, ErrorSeverity.Warning));
            }
        }

        private static void StopTool(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { }
            catch (Win32Exception exception)
            {
                CompilePalLogger.LogDebug($"Could not stop SourceCubemap: {exception.Message}");
            }
        }
    }
}
