using System.Diagnostics;
using System.Text;
using CompilePalX;
using CompilePalX.Compilers;
using CompilePalX.Compiling;

internal static class LauncherTests
{
    public static int FakeCompiler(string[] args)
    {
        switch (args[0])
        {
            case "echo":
                Console.WriteLine("CWD=" + Environment.CurrentDirectory);
                Console.WriteLine("ARG=" + args[1]);
                return 0;
            case "flood":
                Console.Error.Write(new string('E', 1024 * 1024));
                Console.Error.WriteLine("STDERR-COMPLETE");
                Console.WriteLine("STDOUT-COMPLETE");
                return 0;
            case "exit":
                return int.Parse(args[1]);
            case "sleep":
                File.WriteAllText(args[1], Environment.ProcessId.ToString());
                Thread.Sleep(TimeSpan.FromMinutes(2));
                return 0;
            case "tree":
            case "orphan":
                using (var child = Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    ArgumentList = { "--fake-compiler", "sleep", args[1] + ".child" }
                })!)
                {
                    File.WriteAllText(args[1], Environment.ProcessId.ToString());
                    if (args[0] == "orphan")
                        return 0;
                    Thread.Sleep(TimeSpan.FromMinutes(2));
                }
                return 0;
            default:
                return 99;
        }
    }

    public static void Run(string root, Action<bool, string> assert)
    {
        string toolDirectory = Path.Combine(root, "Executable Tools With Spaces");
        Directory.CreateDirectory(toolDirectory);
        foreach (string file in Directory.GetFiles(AppContext.BaseDirectory))
            File.Copy(file, Path.Combine(toolDirectory, Path.GetFileName(file)), true);
        string original = Path.Combine(toolDirectory, "ToolsPlusPlus.Tests.exe");
        string executable = Path.Combine(toolDirectory, "vbsp++.exe");
        File.Copy(original, executable);
        string stockExecutable = Path.Combine(toolDirectory, "vbsp.exe");
        File.Copy(original, stockExecutable);
        string argument = Path.Combine(root, "Map Sources", "sealed test.vmf");
        string customDirectory = Path.Combine(root, "Plugin Working Directory");
        Directory.CreateDirectory(customDirectory);
        var configuration = new GameConfiguration { VBSP = executable, BinFolder = root };
        GameConfigurationManager.GameConfiguration = configuration;
        var context = new CompileContext { Configuration = configuration, MapFile = argument };

        CompileExecutable Create(string path, string arguments, bool checkExit = true, string? workingDirectory = null)
        {
            CompilePalLogger.Reset();
            return new CompileExecutable("VBSP")
            {
                Metadata = new CompileMetadata
                {
                    Name = "VBSP",
                    Path = path,
                    Arguments = "--fake-compiler " + arguments,
                    WorkingDirectory = workingDirectory,
                    CheckExitCode = checkExit
                }
            };
        }

        void RunCompiler(CompileExecutable compiler)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            compiler.Run(context, timeout.Token);
        }

        var compiler = Create("\"" + executable + "\"", "echo $vmfFile$", workingDirectory: "$binFolder$");
        RunCompiler(compiler);
        string output = CompilePalLogger.Output.ToString();
        assert(output.Contains("CWD=" + toolDirectory) && output.Contains("ARG=" + argument)
            && CompilePalLogger.Errors.Count == 0, "Launcher executes quoted Tools++ path and map argument with spaces");

        compiler = Create(executable, "echo $vmfFile$", workingDirectory: customDirectory);
        RunCompiler(compiler);
        assert(CompilePalLogger.Output.ToString().Contains("CWD=" + customDirectory),
            "Launcher preserves explicit plugin working directory");

        compiler = Create(stockExecutable, "echo $vmfFile$", workingDirectory: "$binFolder$");
        RunCompiler(compiler);
        assert(CompilePalLogger.Output.ToString().Contains("CWD=" + root),
            "Stock compiler preserves game bin working directory");

        compiler = Create(executable, "flood");
        RunCompiler(compiler);
        output = CompilePalLogger.Output.ToString();
        assert(output.Contains("STDERR-COMPLETE") && output.Contains("STDOUT-COMPLETE"),
            "Launcher drains more than a pipe buffer of stderr without deadlock");

        compiler = Create(executable, "exit 17", checkExit: false);
        RunCompiler(compiler);
        assert(CompilePalLogger.Errors.Any(error => error.Severity == ErrorSeverity.FatalError
            && error.Description.Contains("17")), "Tools++ failure is fatal despite inherited CheckExitCode=false");

        bool allToolFailuresDetected = true;
        foreach (string name in new[] { "vvis", "vrad", "bspzip" })
        {
            string tool = Path.Combine(toolDirectory, name + "++.exe");
            File.Copy(original, tool);
            compiler = Create(tool, "exit 23", checkExit: false);
            RunCompiler(compiler);
            allToolFailuresDetected &= CompilePalLogger.Errors.Any(error => error.Severity == ErrorSeverity.FatalError);
        }
        assert(allToolFailuresDetected, "VVIS++, VRAD++ and BSPZIP++ failures are fatal with inherited exit-check opt-out");

        configuration.SteamAppID = 4000;
        configuration.BSPZip = stockExecutable;
        compiler = Create(stockExecutable, "echo $vmfFile$");
        compiler.Metadata.Name = "REPACK";
        RunCompiler(compiler);
        assert(CompilePalLogger.Errors.Any(error => error.Severity == ErrorSeverity.FatalError)
            && CompilePalLogger.Output.Length == 0, "GMod REPACK rejects stock packer before launching it");

        configuration.BSPZip = Path.Combine(toolDirectory, "bspzip++.exe");
        compiler = Create(configuration.BSPZip, "echo $vmfFile$");
        compiler.Metadata.Name = "REPACK";
        RunCompiler(compiler);
        assert(CompilePalLogger.Errors.Count == 0 && CompilePalLogger.Output.Length != 0,
            "GMod REPACK launches an existing BSPZIP++ tool");
        configuration.SteamAppID = null;

        compiler = Create(stockExecutable, "exit 17", checkExit: false);
        RunCompiler(compiler);
        assert(CompilePalLogger.Errors.Count == 0, "Stock explicit CheckExitCode=false remains supported");

        compiler = Create(stockExecutable, "exit 17");
        RunCompiler(compiler);
        assert(CompilePalLogger.Errors.Any(error => error.Severity == ErrorSeverity.FatalError),
            "Checked nonzero exit prevents successful later stages");

        bool fastExitsSucceeded = true;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            compiler = Create(executable, "exit 0");
            RunCompiler(compiler);
            fastExitsSucceeded &= CompilePalLogger.Errors.Count == 0;
        }
        assert(fastExitsSucceeded, "Fast successful compiler exits tolerate priority assignment races");

        compiler = Create(Path.Combine(toolDirectory, "missing.exe"), "exit 0");
        RunCompiler(compiler);
        assert(CompilePalLogger.Errors.Any(error => error.Severity == ErrorSeverity.FatalError),
            "Missing executable produces a fatal launch error");

        compiler = Create(executable, "exit 0");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            bool cancellationObserved = false;
            try { compiler.Run(context, cancelled.Token); }
            catch (OperationCanceledException) { cancellationObserved = true; }
            assert(cancellationObserved && compiler.Process is null, "Pre-cancelled launcher does not start a process");
        }

        string marker = Path.Combine(root, "launcher-process");
        compiler = Create(executable, "tree \"" + marker + "\"");
        CheckCancellation(token => compiler.Run(context, token), marker, assert, "Launcher");

        compiler = Create(executable, "exit 0");
        using (var completed = new CancellationTokenSource())
        {
            compiler.Run(context, completed.Token);
            completed.Cancel();
            assert(compiler.Process is null, "Completed launcher releases process and cancellation registration");
        }

        var utilityOutput = new StringBuilder();
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
        {
            ToolProcessRunner.Run(StartInfo(executable, "flood"), timeout.Token, text => utilityOutput.Append(text));
        }
        assert(utilityOutput.ToString().Contains("STDERR-COMPLETE") && utilityOutput.ToString().Contains("STDOUT-COMPLETE"),
            "PACK utility runner drains stdout and stderr concurrently");

        bool utilityFailure = false;
        try { ToolProcessRunner.Run(StartInfo(executable, "exit", "19"), CancellationToken.None); }
        catch (IOException exception) { utilityFailure = exception.Message.Contains("19"); }
        assert(utilityFailure, "PACK utility runner propagates nonzero exit before output copying");

        marker = Path.Combine(root, "utility-process");
        CheckCancellation(token => ToolProcessRunner.Run(StartInfo(executable, "tree", marker), token),
            marker, assert, "PACK utility");
        CheckInheritedPipeCancellation(executable, Path.Combine(root, "orphaned-utility-process"), assert);
    }

    private static void CheckInheritedPipeCancellation(string executable, string marker, Action<bool, string> assert)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Task<bool> task = Task.Run(() =>
        {
            try
            {
                ToolProcessRunner.Run(StartInfo(executable, "orphan", marker), cancellation.Token);
                return false;
            }
            catch (OperationCanceledException) { return true; }
        });
        int parent = 0;
        int child = 0;
        try
        {
            if (!SpinWait.SpinUntil(() => TryReadProcessId(marker, out parent)
                && TryReadProcessId(marker + ".child", out child) && HasExited(parent), TimeSpan.FromSeconds(10)))
                throw new InvalidOperationException("Fake utility did not leave an orphaned child holding its output pipes.");
            bool inheritedPipesStillOpen = !task.IsCompleted && !HasExited(child);
            cancellation.Cancel();
            bool observed = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            assert(inheritedPipesStillOpen && observed,
                "Utility cancellation interrupts inherited output pipes after the parent process exits");
        }
        finally
        {
            cancellation.Cancel();
            if (child != 0 && !HasExited(child))
            {
                using var process = Process.GetProcessById(child);
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
    }

    private static ProcessStartInfo StartInfo(string executable, params string[] arguments)
    {
        var result = new ProcessStartInfo(executable);
        result.ArgumentList.Add("--fake-compiler");
        foreach (string argument in arguments)
            result.ArgumentList.Add(argument);
        return result;
    }

    internal static void CheckCancellation(Action<CancellationToken> run, string marker,
        Action<bool, string> assert, string label)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Task<bool> task = Task.Run(() =>
        {
            try { run(cancellation.Token); return false; }
            catch (OperationCanceledException) { return true; }
        });
        try
        {
            int parent = 0;
            int child = 0;
            if (!SpinWait.SpinUntil(() => TryReadProcessId(marker, out parent)
                && TryReadProcessId(marker + ".child", out child), TimeSpan.FromSeconds(10)))
                throw new InvalidOperationException(label + " fake process tree did not start.");
            cancellation.Cancel();
            bool observed = task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            bool stopped = SpinWait.SpinUntil(() => HasExited(parent) && HasExited(child), TimeSpan.FromSeconds(5));
            assert(observed && stopped, label + " cancellation terminates parent and child processes");
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    private static bool TryReadProcessId(string path, out int id)
    {
        id = 0;
        try { return int.TryParse(File.ReadAllText(path), out id); }
        catch (IOException) { return false; }
    }

    private static bool HasExited(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            return process.HasExited;
        }
        catch (ArgumentException) { return true; }
    }
}
