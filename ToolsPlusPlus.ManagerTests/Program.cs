using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CompilePalX;
using CompilePalX.Compilers;
using CompilePalX.Configuration;

internal static class Program
{
    private static int passed;
    private static int starts;
    private static int finishes;

    [STAThread]
    private static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources["CompilePal.Brushes.Success"] = Brushes.Green;
        app.Resources["CompilePal.Brushes.Severity4"] = Brushes.Red;
        MainWindow.ActiveDispatcher = Dispatcher.CurrentDispatcher;
        CompilingManager.OnClear += () => { };
        CompilingManager.OnStart += () => starts++;
        CompilingManager.OnFinish += () => finishes++;
        try
        {
            CancellationWaitsForWorker();
            FailureStopsPipeline(false);
            FailureStopsPipeline(true);
            SuccessfulPipeline();
            Console.WriteLine($"{passed} manager lifecycle checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally { app.Shutdown(); }
    }

    private static void CancellationWaitsForWorker()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int laterStages = 0;
        var original = Prepare((_, token) =>
        {
            GameConfigurationManager.GameConfiguration = new() { Name = "Temporary context" };
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("The cancellation test did not release the stage.");
        }, (_, _) => Interlocked.Increment(ref laterStages));
        CompilingManager.StartCompile();
        PumpUntil(() => entered.IsSet);
        CompilingManager.CancelCompile();
        Check(CompilingManager.IsCompiling, "Cancellation keeps the manager busy until the worker exits");
        Check(finishes == 0, "Cancellation does not send an early finish event");
        CompilingManager.StartCompile();
        Check(starts == 1, "A second compile cannot start during cancellation");
        release.Set();
        PumpUntil(() => finishes == 1);
        Check(laterStages == 0, "Cancellation skips remaining stages");
        AssertStopped(original, "Cancellation");
    }

    private static void FailureStopsPipeline(bool unexpected)
    {
        int laterStages = 0;
        var original = Prepare((_, _) =>
        {
            GameConfigurationManager.GameConfiguration = new() { Name = "Temporary context" };
            if (unexpected)
                throw new IOException("Intentional stage exception");
            CompilePalLogger.LogCompileError("Intentional fatal error", new Error { Severity = 5 });
        }, (_, _) => Interlocked.Increment(ref laterStages));
        CompilingManager.StartCompile();
        PumpUntil(() => finishes == 1);
        string scenario = unexpected ? "Unexpected exception" : "Fatal error";
        Check(laterStages == 0, scenario + " skips remaining stages");
        AssertStopped(original, scenario);
        if (unexpected)
            Check(CompilePalLogger.Lines.Any(line => line.Contains("Intentional stage exception")), "Unexpected exceptions are logged");
    }

    private static void SuccessfulPipeline()
    {
        int stages = 0;
        var original = Prepare((_, _) => Interlocked.Increment(ref stages), (_, _) => Interlocked.Increment(ref stages));
        CompilingManager.StartCompile();
        PumpUntil(() => finishes == 1);
        Check(stages == 2, "A successful compile executes every stage");
        Check(!CompilingManager.IsCompiling, "A successful compile returns to idle");
        Check(ReferenceEquals(original, GameConfigurationManager.GameConfiguration) && GameConfigurationManager.Restores == 1,
            "A successful compile restores the configuration once");
        Check(CompilePalLogger.Lines.Count(line => line.Contains("compile finished")) == 1, "A successful compile logs completion once");
        Check(!ProgressManager.HasError, "A successful compile keeps the success state");
    }

    private static GameConfiguration Prepare(params Action<CompileContext, CancellationToken>[] handlers)
    {
        starts = 0;
        finishes = 0;
        CompilePalLogger.Lines.Clear();
        var original = new GameConfiguration();
        GameConfigurationManager.GameConfiguration = original;
        GameConfigurationManager.Restores = 0;
        var preset = new Preset();
        var processes = handlers.Select(handler => new CompileProcess { Handler = handler }).ToList();
        foreach (var process in processes)
            process.PresetDictionary[preset] = new object();
        ConfigurationManager.CurrentPreset = preset;
        ConfigurationManager.CompileProcesses = processes;
        OrderManager.CurrentOrder = processes;
        CompilingManager.MapFiles.Clear();
        CompilingManager.MapFiles.Add(new Map("fixture.vmf", preset: preset));
        return original;
    }

    private static void AssertStopped(GameConfiguration original, string scenario)
    {
        Check(!CompilingManager.IsCompiling, scenario + " returns to idle after the worker exits");
        Check(ReferenceEquals(original, GameConfigurationManager.GameConfiguration) && GameConfigurationManager.Restores == 1,
            scenario + " restores the configuration once");
        Check(!CompilePalLogger.Lines.Any(line => line.Contains("compile finished")), scenario + " never logs successful completion");
        Check(ProgressManager.HasError, scenario + " marks the progress as unsuccessful");
    }

    private static void PumpUntil(Func<bool> predicate)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(10) };
        var elapsed = Stopwatch.StartNew();
        var frame = new DispatcherFrame();
        timer.Tick += (_, _) => { if (predicate() || elapsed.Elapsed > TimeSpan.FromSeconds(15)) frame.Continue = false; };
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        if (!predicate())
            throw new TimeoutException("The compile manager did not reach the expected state.");
    }

    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException(description);
        passed++;
        Console.WriteLine("PASS " + description);
    }
}
