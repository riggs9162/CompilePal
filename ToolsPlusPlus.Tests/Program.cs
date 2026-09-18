using CompilePalX;
using System.Text;

internal static class Program
{
    private static int count;

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
        count++;
    }

    private static void Touch(string directory, params string[] names)
    {
        Directory.CreateDirectory(directory);
        foreach (string name in names) File.WriteAllText(Path.Combine(directory, name), string.Empty);
    }

    private static string KvPath(string path) => path.Replace('\\', '/');

    public static int Main(string[] args)
    {
        if (args.Length != 0 && args[0] == "--fake-compiler")
            return LauncherTests.FakeCompiler(args[1..]);
        if (CubemapTests.TryRunFakeTool(args, out int fakeExitCode))
            return fakeExitCode;
        if (args.Length != 0 && args[0] == "--verify-vbspinfo")
            return CubemapTests.VerifyRealUtility(args);
        string root = Path.Combine(Path.GetTempPath(), "CompilePal tools++ tests " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string? previousEnvironmentValue = Environment.GetEnvironmentVariable("COMPILEPAL_TOOL_TEST");
        try
        {
            string gameBin = Path.Combine(root, "Game One", "bin");
            string tools = Path.Combine(root, "Standalone Tools++");
            Touch(gameBin, "vbsp.exe", "bspzip.exe", "vbspinfo.exe", "vpk.exe");
            Touch(tools, "vbsp++.exe", "vvis++.exe", "vrad++.exe", "bspzip++.exe");
            string vbsp = Path.Combine(tools, "vbsp++.exe");
            string packer = Path.Combine(tools, "bspzip++.exe");

            Assert(ToolsPlusPlusPaths.FindCompanion("bspzip", gameBin, vbsp) == packer,
                "Standalone VBSP selects adjacent BSPZIP++ before stock packer");
            Assert(ToolsPlusPlusPaths.FindCompanion("bspzip", gameBin, Path.Combine(gameBin, "vbsp.exe"))
                == Path.Combine(gameBin, "bspzip.exe"), "Stock configuration keeps stock packer");
            Assert(ToolsPlusPlusPaths.FindCompanion("vbspinfo", gameBin, vbsp)
                == Path.Combine(gameBin, "vbspinfo.exe"), "VBSPInfo stays in game bin");
            Assert(ToolsPlusPlusPaths.FindCompanion("vpk", gameBin, vbsp)
                == Path.Combine(gameBin, "vpk.exe"), "VPK stays in game bin");
            Assert(ToolsPlusPlusPaths.FindCompanion("vbspinfo", null, null) is null,
                "Missing optional utilities do not throw");
            Assert(ToolsPlusPlusPaths.Clean("  \"" + vbsp + "\"  ") == vbsp, "Quoted executable path is cleaned");
            Assert(ToolsPlusPlusPaths.Resolve("../maps", gameBin)
                == Path.GetFullPath(Path.Combine(gameBin, "../maps")), "Parent-relative path resolves");
            Assert(ToolsPlusPlusPaths.Resolve("maps", gameBin) == Path.Combine(gameBin, "maps"),
                "Ordinary relative path resolves");
            Assert(ToolsPlusPlusPaths.Resolve(null, gameBin) == string.Empty, "Missing path remains empty");
            Environment.SetEnvironmentVariable("COMPILEPAL_TOOL_TEST", tools);
            Assert(ToolsPlusPlusPaths.Resolve("%COMPILEPAL_TOOL_TEST%/vrad++.exe", gameBin)
                == Path.Combine(tools, "vrad++.exe"), "Environment variable path resolves");
            Assert(ToolsPlusPlusPaths.TrySelectSuite(tools, out var suite, out _) && suite.Count == 4,
                "Full standalone suite can be selected");
            File.Delete(packer);
            Assert(!ToolsPlusPlusPaths.TrySelectSuite(tools, out var partial, out string error)
                && partial.Count == 0 && error.Contains("bspzip++.exe"), "Partial suite cannot partially update paths");
            Assert(ToolsPlusPlusPaths.FindCompanion("bspzip", gameBin, vbsp) == Path.Combine(gameBin, "bspzip.exe"),
                "Missing standalone packer falls back to available stock packer");
            Touch(tools, "bspzip++.exe");
            Assert(ToolsPlusPlusPaths.WorkingDirectory(vbsp, gameBin) == tools,
                "Standalone compiler uses its own directory");
            Assert(ToolsPlusPlusPaths.WorkingDirectory(Path.Combine(gameBin, "vbsp.exe"), gameBin) == gameBin,
                "Stock working directory is unchanged");
            Assert(ToolsPlusPlusPaths.WorkingDirectory(vbsp, "custom", false) == "custom",
                "Explicit custom working directory is preserved");
            Assert(ToolsPlusPlusPaths.SupportsGModRepack(packer), "Existing BSPZIP++ permits GMod REPACK");
            Assert(!ToolsPlusPlusPaths.SupportsGModRepack(Path.Combine(gameBin, "bspzip.exe")),
                "Stock GMod BSPZIP does not permit REPACK");
            Assert(!ToolsPlusPlusPaths.SupportsGModRepack(Path.Combine(root, "missing", "bspzip++.exe")),
                "Missing executable does not claim REPACK support");
            Assert(ToolsPlusPlusPaths.IsPlusPlus("VRAD++.EXE", "vrad"), "Plus-plus filename matching is case insensitive");
            Assert(!ToolsPlusPlusPaths.IsPlusPlus("my-vrad++.exe", "vrad"), "Similar third-party filename is not detected");

            string configDir = Path.Combine(gameBin, "hammerplusplus");
            Directory.CreateDirectory(configDir);
            string gameFolder = Path.Combine(root, "Game One", "game");
            Directory.CreateDirectory(gameFolder);
            File.WriteAllText(Path.Combine(gameFolder, "gameinfo.txt"),
                "\"GameInfo\" { \"FileSystem\" { \"SteamAppId\" \"4000\" } }");
            string secondGame = Path.Combine(root, "Game One", "secondgame");
            Directory.CreateDirectory(secondGame);
            string GameBlock(string name, string bspPath, string gamePath) =>
                $"\"{name}\" {{ \"GameDir\" \"{KvPath(gamePath)}\" \"Hammer\" {{ " +
                $"\"BSP\" \"{KvPath(bspPath)}\" \"Vis\" \"vvis.exe\" \"Light\" \"vrad.exe\" " +
                "\"GameExe\" \"../hl2.exe\" \"MapDir\" \"../mapsrc\" \"BSPDir\" \"../game/maps\" } }";
            string configText = "\"Configs\" { \"Games\" { "
                + GameBlock("Standalone", vbsp, gameFolder)
                + GameBlock("Stock", Path.Combine(gameBin, "vbsp.exe"), secondGame) + " } }";
            File.WriteAllText(Path.Combine(configDir, "hammerplusplus_gameconfig.txt"), configText, Encoding.UTF8);
            var parsed = GameConfigurationParser.Parse(gameBin);
            Assert(parsed.Count == 2, "Both Hammer games import successfully");
            Assert(parsed.All(g => g.BinFolder == gameBin), "Companion discovery cannot mutate shared game bin");
            Assert(parsed[0].BSPZip == packer, "Real parser selects BSPZIP++ for standalone game");
            Assert(parsed[1].BSPZip == Path.Combine(gameBin, "bspzip.exe"), "Second game keeps stock packer");
            Assert(parsed[0].SteamAppID == 4000, "Steam app ID still imports");
            Assert(parsed[1].VVIS == Path.Combine(gameBin, "vvis.exe"), "Relative Hammer compiler path resolves");
            File.Delete(Path.Combine(gameBin, "bspzip.exe"));
            File.Delete(packer);
            parsed = GameConfigurationParser.Parse(gameBin);
            Assert(parsed.Count == 2 && parsed.All(g => g.BinFolder == gameBin && g.BSPZip == string.Empty),
                "No packer available does not lose games or corrupt bin paths");
            LauncherTests.Run(root, Assert);
            CubemapTests.Run(root, Assert);
            Console.WriteLine($"All {count} regression checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPILEPAL_TOOL_TEST", previousEnvironmentValue);
            Directory.Delete(root, true);
        }
    }
}
