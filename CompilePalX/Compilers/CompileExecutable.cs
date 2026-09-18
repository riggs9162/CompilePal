using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CompilePalX.Compiling;

namespace CompilePalX.Compilers
{
    class CompileExecutable(string metadata, string? parameterFolder = null) : CompileProcess(metadata, parameterFolder)
    {
        public override void Run(CompileContext c, CancellationToken cancellationToken)
        {
            CompileErrors = [];

            if (!CanRun(c)) return;

            if (Name == "REPACK" && c.Configuration.SteamAppID == 4000
                && !ToolsPlusPlusPaths.SupportsGModRepack(c.Configuration.BSPZip))
            {
                CompilePalLogger.LogCompileError(
                    "Garry's Mod REPACK requires an existing bspzip++.exe. Select the Tools++ folder in Game Configuration.\n",
                    new Error("Garry's Mod REPACK requires BSPZIP++", ErrorSeverity.FatalError));
                return;
            }

            using var process = new Process();
            Process = process;
            try
            {
                if (Metadata.ReadOutput)
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                }

                var args = GameConfigurationManager.SubstituteValues(GetParameterString(), c.MapFile);
                bool normalPriority = args.Contains("-normal_priority");
                if (normalPriority)
                    args = args.Replace("-normal_priority", string.Empty);

                process.StartInfo.FileName = ToolsPlusPlusPaths.Clean(
                    GameConfigurationManager.SubstituteValues(Metadata.Path, quote: false));
                process.StartInfo.Arguments = args;
                string workingDirectory = Metadata.WorkingDirectory != null
                    ? GameConfigurationManager.SubstituteValues(Metadata.WorkingDirectory, quote: false)
                    : ".";
                bool usesDefaultDirectory = string.IsNullOrEmpty(Metadata.WorkingDirectory)
                    || Metadata.WorkingDirectory == "$binFolder$" || Metadata.WorkingDirectory == ".";
                process.StartInfo.WorkingDirectory = ToolsPlusPlusPaths.WorkingDirectory(
                    process.StartInfo.FileName, workingDirectory, usesDefaultDirectory);

                CompilePalLogger.LogLineDebug($"Running '{process.StartInfo.FileName}' with args '{process.StartInfo.Arguments}'");
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    process.Start();
                }
                catch (Exception exception)
                {
                    CompilePalLogger.LogDebug(exception.ToString());
                    CompilePalLogger.LogCompileError($"Failed to run executable: {process.StartInfo.FileName}\n",
                        new Error($"Failed to run executable: {process.StartInfo.FileName}", ErrorSeverity.FatalError));
                    return;
                }

                using var registration = cancellationToken.Register(() => StopProcess(process));
                SetPriority(process, normalPriority);

                if (Metadata.ReadOutput)
                {
                    Task<string> errorOutput = process.StandardError.ReadToEndAsync(cancellationToken);
                    ReadOutput(process, cancellationToken);
                    string errors = errorOutput.GetAwaiter().GetResult();
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!string.IsNullOrWhiteSpace(errors))
                        CompilePalLogger.LogProgressive(errors);

                    bool checkExitCode = Metadata.CheckExitCode
                        || ToolsPlusPlusPaths.IsPlusPlus(process.StartInfo.FileName, "vbsp")
                        || ToolsPlusPlusPaths.IsPlusPlus(process.StartInfo.FileName, "vvis")
                        || ToolsPlusPlusPaths.IsPlusPlus(process.StartInfo.FileName, "vrad")
                        || ToolsPlusPlusPaths.IsPlusPlus(process.StartInfo.FileName, "bspzip");
                    if (checkExitCode && process.ExitCode != 0)
                        CompilePalLogger.LogCompileError($"{Name} exited with code: {process.ExitCode} (0x{process.ExitCode:X})\n",
                            new Error($"{Name} exited with code: {process.ExitCode} (0x{process.ExitCode:X})", ErrorSeverity.FatalError));
                }
            }
            finally
            {
                Process = null;
            }
        }

        private void SetPriority(Process process, bool normalPriority)
        {
            try
            {
                process.PriorityClass = normalPriority ? ProcessPriorityClass.Normal : ProcessPriorityClass.BelowNormal;
                if (normalPriority)
                    CompilePalLogger.LogLine($"Running {Name} with normal priority");
            }
            catch (InvalidOperationException) { }
            catch (Win32Exception exception)
            {
                CompilePalLogger.LogDebug($"Could not set {Name} process priority: {exception.Message}");
            }
        }

        private static void StopProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
            }
            catch (InvalidOperationException) { }
            catch (Win32Exception exception)
            {
                CompilePalLogger.LogDebug($"Could not stop compiler process: {exception.Message}");
            }
        }

        private static void ReadOutput(Process process, CancellationToken cancellationToken)
        {
            char[] buffer = new char[256];
            while (true)
            {
                int length = process.StandardOutput.ReadAsync(buffer.AsMemory(), cancellationToken)
                    .AsTask().GetAwaiter().GetResult();
                if (length == 0)
                    break;
                CompilePalLogger.LogProgressive(new string(buffer, 0, length));
            }
            process.WaitForExitAsync(cancellationToken).GetAwaiter().GetResult();
        }
    }
}
