using CompilePalX.Compiling;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace CompilePalX.Compilers
{
    class CubemapProcess : CompileProcess
    {
        public CubemapProcess() : base("CUBEMAPS") { }

        bool HDR;
        bool LDR;
        string vbspInfo = string.Empty;
        string bspFile = string.Empty;

        public override void Run(CompileContext context, CancellationToken cancellationToken)
        {
            CompileErrors = [];
            if (!CanRun(context)) return;

            try
            {
                CompilePalLogger.LogLine("\nCompilePal++ - Cubemap Generator", 900);
                vbspInfo = ToolsPlusPlusPaths.Resolve(context.Configuration.VBSPInfo, ".");
                bspFile = ToolsPlusPlusPaths.Resolve(context.CopyLocation, ".");
                if (!File.Exists(bspFile))
                    throw new FileNotFoundException("Could not find the BSP for CUBEMAPS.", bspFile);

                string parameters = GetParameterString();
                bool hidden = Regex.IsMatch(parameters, @"(?:^|\s)-hidden(?=\s|$)");
                string additionalParameters = Regex.Replace(parameters, @"(?:^|\s)-hidden(?=\s|$)", " ");
                string buildCubemapCommand = "-buildcubemaps";
                Match iterationsParameter = Regex.Match(additionalParameters, @"(?:^|\s)-iterations(?:\s+(\S+))?");
                if (iterationsParameter.Success)
                {
                    if (!int.TryParse(iterationsParameter.Groups[1].Value, out int iterations) || iterations < 1)
                        throw new ArgumentException("-iterations must be a positive integer.");
                    buildCubemapCommand += $" {iterations}";
                    additionalParameters = additionalParameters.Remove(iterationsParameter.Index, iterationsParameter.Length);
                }

                FetchHDRLevels(cancellationToken);
                string mapName = Path.GetFileNameWithoutExtension(bspFile);
                string gameFolder = ToolsPlusPlusPaths.Resolve(context.Configuration.GameFolder, ".");
                string arguments = $"-steam -game \"{gameFolder}\" -windowed -insecure -novid +mat_specular 0 "
                    + $"+mat_hdr_level %HDRLEVEL% +map \"{mapName}\" {buildCubemapCommand} {additionalParameters}";
                if (hidden)
                    arguments += " -noborder -x 4000 -y 2000";

                if (LDR)
                {
                    CompilePalLogger.LogLine("Compiling LDR cubemaps...");
                    RunCubemaps(context.Configuration.GameEXE, arguments.Replace("%HDRLEVEL%", "0"), cancellationToken);
                }
                if (HDR)
                {
                    CompilePalLogger.LogLine("Compiling HDR cubemaps...");
                    RunCubemaps(context.Configuration.GameEXE, arguments.Replace("%HDRLEVEL%", "2"), cancellationToken);
                }
                cancellationToken.ThrowIfCancellationRequested();
                CompilePalLogger.LogLine("Cubemaps compiled");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                CompilePalLogger.LogDebug(exception.ToString());
                CompilePalLogger.LogCompileError($"CUBEMAPS failed: {exception.Message}\n",
                    new Error($"CUBEMAPS failed: {exception.Message}", ErrorSeverity.FatalError));
            }
        }

        public void RunCubemaps(string gameEXE, string args, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string executable = ToolsPlusPlusPaths.Resolve(gameEXE, ".");
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(executable, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(executable) ?? ".",
                }
            };
            Process = process;
            try
            {
                process.Start();
                using var registration = cancellationToken.Register(() => StopGame(process));
                process.WaitForExit();
                cancellationToken.ThrowIfCancellationRequested();
                if (process.ExitCode != 0)
                    throw new IOException($"The game exited with code {process.ExitCode} while building cubemaps.");
            }
            finally
            {
                Process = null;
            }
        }

        public void FetchHDRLevels(CancellationToken cancellationToken = default)
        {
            HDR = false;
            LDR = false;
            CompilePalLogger.LogLine("Detecting HDR levels...");
            if (!File.Exists(vbspInfo))
                throw new FileNotFoundException(
                    "CUBEMAPS requires the game's VBSPInfo utility. Configure an existing VBSPInfo executable; the standalone Tools++ suite does not include it.",
                    vbspInfo);

            var startInfo = new ProcessStartInfo(vbspInfo);
            startInfo.ArgumentList.Add(bspFile);
            string output = ToolProcessRunner.Run(startInfo, cancellationToken, CompilePalLogger.LogDebug);
            LDR = ReadLightingCount(output, "LDR") > 0;
            HDR = ReadLightingCount(output, "HDR") > 0;
            if (!LDR && !HDR)
                throw new IOException("VBSPInfo reported no LDR or HDR lighting. Compile lighting before building cubemaps.");
        }

        private static long ReadLightingCount(string output, string mode)
        {
            Match lightData = Regex.Match(output, @"^\s*" + mode + @"\s+lightdata\s+\[variable\]\s+(\d+)\s*/",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            Match worldLights = Regex.Match(output, @"^\s*" + mode + @"\s+worldlights\s+(\d+)\s*/",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            Match match = lightData.Success ? lightData : worldLights;
            if (!match.Success || !long.TryParse(match.Groups[1].Value, out long count))
                throw new IOException($"VBSPInfo did not report valid {mode} lighting statistics. CUBEMAPS requires a compatible VBSPInfo utility and an uncompressed BSP.");
            return count;
        }

        private static void StopGame(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { }
            catch (Win32Exception exception)
            {
                CompilePalLogger.LogDebug($"Could not stop the cubemap game process: {exception.Message}");
            }
        }
    }
}
