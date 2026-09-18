using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CompilePalX;

internal static class WorkspacePreflight
{
    public static List<string> Check(GameConfiguration game, IEnumerable<Map> maps, IEnumerable<CompileProcess> processes)
    {
        var results = new List<string>();
        var included = maps.Where(map => map.Compile).ToList();
        if (included.Count == 0) results.Add("Include at least one map in the queue.");
        foreach (var map in included)
        {
            if (!File.Exists(map.File)) results.Add($"Map not found: {map.File}");
            if (map.Preset == null) results.Add($"Choose a preset for {map.FullMapName}.");
        }
        var enabled = processes.Where(process => process.DoRun && included.Any(map => map.Preset != null && process.PresetDictionary.ContainsKey(map.Preset))).ToList();
        if (included.Count > 0 && enabled.Count == 0) results.Add("The included maps have no enabled compile stages.");
        if (!Directory.Exists(ToolsPlusPlusPaths.Clean(game.GameFolder))) results.Add("Game folder was not found. Open Game & tools to check it.");
        var required = new Dictionary<string, string?>();
        foreach (var process in enabled)
        {
            switch (process.Name)
            {
                case "VBSP": required["VBSP"] = game.VBSP; break;
                case "VVIS": required["VVIS"] = game.VVIS; break;
                case "VRAD": required["VRAD"] = game.VRAD; break;
                case "PACK": required["BSPZIP"] = game.BSPZip; break;
                case "REPACK": required["BSPZIP"] = game.BSPZip; break;
                case "CUBEMAPS": required["VBSPInfo"] = game.VBSPInfo; required["Game executable"] = game.GameEXE; break;
            }
        }
        foreach (var tool in required)
        {
            if (!File.Exists(ToolsPlusPlusPaths.Clean(tool.Value))) results.Add($"{tool.Key}: executable not found. Check its path in Game & tools.");
        }
        foreach (var map in included.Where(map => map.Preset != null))
        {
            var stages = enabled.Where(process => process.PresetDictionary.ContainsKey(map.Preset!)).OrderBy(process => process.Ordering).ToList();
            int repack = stages.FindIndex(stage => stage.Name == "REPACK");
            if (repack >= 0 && stages.Skip(repack + 1).Any(stage => stage.Name is "PACK" or "CUBEMAPS"))
                results.Add($"{map.FullMapName}: put PACK and CUBEMAPS before REPACK when compressing the BSP.");
            foreach (var stage in stages)
                foreach (var option in stage.PresetDictionary[map.Preset!].Where(option => option.CanHaveValue && string.IsNullOrWhiteSpace(option.Value)))
                    results.Add($"{map.FullMapName} / {stage.Name}: enter a value for {option.Name}, or disable it.");
        }
        return results.Distinct().ToList();
    }
}
