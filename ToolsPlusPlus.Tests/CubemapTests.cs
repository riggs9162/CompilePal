using System.Text.Json;
using CompilePalX;
using CompilePalX.Compilers;
using CompilePalX.Compiling;

internal static class CubemapTests
{
    public static int VerifyRealUtility(string[] args)
    {
        if (args.Length != 4 || !int.TryParse(args[3], out int expectedPasses))
            throw new ArgumentException("Usage: --verify-vbspinfo <vbspinfo.exe> <map.bsp> <expected passes; 0 expects failure>");
        string root = Path.Combine(Path.GetTempPath(), "CompilePal real utility test " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (string file in Directory.GetFiles(AppContext.BaseDirectory))
                File.Copy(file, Path.Combine(root, Path.GetFileName(file)), true);
            string game = Path.Combine(root, "fake-cubemap-game.exe");
            File.Copy(Path.Combine(root, "ToolsPlusPlus.Tests.exe"), game);
            var context = new CompileContext
            {
                CopyLocation = "\"" + Path.GetFullPath(args[2]) + "\"",
                Configuration = new GameConfiguration
                {
                    VBSPInfo = "\"" + Path.GetFullPath(args[1]) + "\"",
                    GameEXE = "\"" + game + "\"",
                    GameFolder = root
                }
            };
            CompilePalLogger.Reset();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            new CubemapProcess().Run(context, timeout.Token);
            string[][] runs = ReadRuns(Path.Combine(root, "runs.jsonl"));
            bool correct = expectedPasses == 0
                ? runs.Length == 0 && FatalWithoutSuccess()
                : runs.Length == expectedPasses && CompilePalLogger.Errors.Count == 0;
            Console.WriteLine($"Real VBSPInfo: {runs.Length} game passes, {CompilePalLogger.Errors.Count} errors; expected passes {expectedPasses}.");
            foreach (Error error in CompilePalLogger.Errors)
                Console.WriteLine(error.Description);
            return correct ? 0 : 1;
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    public static bool TryRunFakeTool(string[] args, out int exitCode)
    {
        exitCode = 0;
        switch (Path.GetFileName(Environment.ProcessPath))
        {
            case "fake-vbspinfo.exe":
                exitCode = RunFakeInfo(args);
                return true;
            case "fake-cubemap-game.exe":
                exitCode = RunFakeGame(args);
                return true;
            default:
                return false;
        }
    }

    private static int RunFakeInfo(string[] args)
    {
        if (args.Length != 1)
            return 90;
        string mode = File.ReadAllText(args[0]);
        File.WriteAllText(args[0] + ".info-args.json", JsonSerializer.Serialize(args));
        if (mode.StartsWith("sleep|"))
            return LauncherTests.FakeCompiler(["tree", mode["sleep|".Length..]]);
        if (mode == "fail")
        {
            Console.Error.WriteLine("Cannot read compressed BSP");
            return 31;
        }
        if (mode == "malformed")
        {
            Console.WriteLine("Unknown statistics format");
            return 0;
        }
        int ldr = mode is "both" or "ldr" ? 2 : 0;
        int hdr = mode is "both" or "hdr" ? 3 : 0;
        Console.WriteLine($"LDR worldlights          {ldr}/8192           88/720896   (0.0%)");
        Console.WriteLine($"HDR worldlights          {hdr}/8192           88/720896   (0.0%)");
        if (mode == "lightdata")
        {
            Console.WriteLine("LDR lightdata         [variable]        9856/0        (0.0%)");
            Console.WriteLine("HDR lightdata         [variable]           0/0        (0.0%)");
        }
        return 0;
    }

    private static int RunFakeGame(string[] args)
    {
        int gameIndex = Array.IndexOf(args, "-game");
        if (gameIndex < 0 || gameIndex + 1 >= args.Length)
            return 91;
        string game = args[gameIndex + 1];
        File.AppendAllText(Path.Combine(game, "runs.jsonl"), JsonSerializer.Serialize(args) + Environment.NewLine);
        File.WriteAllText(Path.Combine(game, "working-directory.txt"), Environment.CurrentDirectory);
        int cancelIndex = Array.IndexOf(args, "--test-cancel");
        if (cancelIndex >= 0)
            return LauncherTests.FakeCompiler(["tree", args[cancelIndex + 1]]);
        string failure = Path.Combine(game, "game-exit-code.txt");
        return File.Exists(failure) ? int.Parse(File.ReadAllText(failure)) : 0;
    }

    public static void Run(string root, Action<bool, string> assert)
    {
        string tools = Path.Combine(root, "Cubemap Tools With Spaces");
        Directory.CreateDirectory(tools);
        foreach (string file in Directory.GetFiles(AppContext.BaseDirectory))
            File.Copy(file, Path.Combine(tools, Path.GetFileName(file)), true);
        string source = Path.Combine(tools, "ToolsPlusPlus.Tests.exe");
        string info = Path.Combine(tools, "fake-vbspinfo.exe");
        string gameExe = Path.Combine(tools, "fake-cubemap-game.exe");
        File.Copy(source, info);
        File.Copy(source, gameExe);
        string game = Path.Combine(root, "Cubemap Game With Spaces");
        Directory.CreateDirectory(game);
        string bsp = Path.Combine(game, "map with spaces.bsp");
        string runLog = Path.Combine(game, "runs.jsonl");
        var configuration = new GameConfiguration
        {
            VBSPInfo = "\"" + info + "\"",
            GameEXE = "\"" + gameExe + "\"",
            GameFolder = "\"" + game + "\""
        };
        var context = new CompileContext { Configuration = configuration, CopyLocation = "\"" + bsp + "\"" };
        var process = new CubemapProcess();
        process.Metadata.Arguments = "-hidden -iterations 12 +fps_max 60";

        string[][] RunCase(string mode)
        {
            File.WriteAllText(bsp, mode);
            File.Delete(runLog);
            CompilePalLogger.Reset();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            process.Run(context, timeout.Token);
            return ReadRuns(runLog);
        }

        string[][] runs = RunCase("both");
        assert(runs.Length == 2 && ValueAfter(runs[0], "+mat_hdr_level") == "0"
            && ValueAfter(runs[1], "+mat_hdr_level") == "2" && CompilePalLogger.Errors.Count == 0,
            "CUBEMAPS runs separate LDR and HDR passes from reported utility statistics");
        assert(runs.All(args => ValueAfter(args, "+map") == "map with spaces")
            && File.ReadAllText(Path.Combine(game, "working-directory.txt")) == tools
            && JsonSerializer.Deserialize<string[]>(File.ReadAllText(bsp + ".info-args.json"))!.SequenceEqual([bsp]),
            "CUBEMAPS accepts quoted tool/game/BSP paths and preserves map names containing spaces");
        assert(runs.All(args => ValueAfter(args, "-buildcubemaps") == "12"
            && ValueAfter(args, "+fps_max") == "60" && !args.Contains("-iterations") && args.Contains("-noborder")),
            "CUBEMAPS preserves extra game arguments and supports multi-digit iterations");

        runs = RunCase("hdr");
        assert(runs.Length == 1 && ValueAfter(runs[0], "+mat_hdr_level") == "2",
            "CUBEMAPS resets previous LDR state and explicitly selects HDR-only mode");
        runs = RunCase("ldr");
        assert(runs.Length == 1 && ValueAfter(runs[0], "+mat_hdr_level") == "0",
            "CUBEMAPS resets previous HDR state and explicitly selects LDR-only mode");
        runs = RunCase("lightdata");
        assert(runs.Length == 1 && ValueAfter(runs[0], "+mat_hdr_level") == "0",
            "CUBEMAPS detects lighting data even when a map has no worldlights");

        configuration.VBSPInfo = Path.Combine(tools, "missing-vbspinfo.exe");
        runs = RunCase("both");
        assert(runs.Length == 0 && FatalWithoutSuccess()
            && CompilePalLogger.Errors[0].Description.Contains("requires the game's VBSPInfo"),
            "Missing VBSPInfo fails CUBEMAPS before launching the game");
        configuration.VBSPInfo = "\"" + info + "\"";

        runs = RunCase("fail");
        assert(runs.Length == 0 && FatalWithoutSuccess() && CompilePalLogger.Errors[0].Description.Contains("31"),
            "Failed VBSPInfo aborts CUBEMAPS instead of guessing a lighting mode");
        runs = RunCase("malformed");
        assert(runs.Length == 0 && FatalWithoutSuccess(),
            "Unrecognized VBSPInfo output cannot produce a successful cubemap step");
        runs = RunCase("empty");
        assert(runs.Length == 0 && FatalWithoutSuccess(),
            "Maps with no reported lighting fail clearly before cubemap generation");

        string failurePath = Path.Combine(game, "game-exit-code.txt");
        File.WriteAllText(failurePath, "29");
        runs = RunCase("both");
        assert(runs.Length == 1 && FatalWithoutSuccess() && CompilePalLogger.Errors[0].Description.Contains("29"),
            "Game failure aborts remaining cubemap passes and suppresses success reporting");
        File.Delete(failurePath);

        File.WriteAllText(bsp, "both");
        string marker = Path.Combine(root, "cubemap-game-process");
        process.Metadata.Arguments = "--test-cancel \"" + marker + "\"";
        CompilePalLogger.Reset();
        LauncherTests.CheckCancellation(token => process.Run(context, token), marker, assert, "CUBEMAPS game");
        assert(!CompilePalLogger.Output.ToString().Contains("Cubemaps compiled") && process.Process is null,
            "Cancelled CUBEMAPS releases its game process and never reports completion");

        process.Metadata.Arguments = string.Empty;
        marker = Path.Combine(root, "cubemap-info-process");
        File.WriteAllText(bsp, "sleep|" + marker);
        CompilePalLogger.Reset();
        LauncherTests.CheckCancellation(token => process.Run(context, token), marker, assert, "CUBEMAPS utility");
        assert(!CompilePalLogger.Output.ToString().Contains("Cubemaps compiled"),
            "Cancelled VBSPInfo cannot advance to game or success reporting");
    }

    private static string[][] ReadRuns(string path) => File.Exists(path)
        ? File.ReadAllLines(path).Select(line => JsonSerializer.Deserialize<string[]>(line)!).ToArray()
        : [];

    private static string? ValueAfter(string[] args, string key)
    {
        int index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static bool FatalWithoutSuccess() =>
        CompilePalLogger.Errors.Any(error => error.Severity == ErrorSeverity.FatalError)
        && !CompilePalLogger.Output.ToString().Contains("Cubemaps compiled");
}
