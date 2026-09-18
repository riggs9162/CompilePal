using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CompilePalX.Compiling;
using ValveKeyValue;

namespace CompilePalX {
    class GameConfigurationParser
    {
        private static KVSerializer KVSerializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        public static List<GameConfiguration> Parse(string binFolder)
        {
            // prioritize hammer++ configs, fallback to hammer if it doesn't exist
            string filename = Path.Combine(binFolder, "hammerplusplus", "hammerplusplus_gameconfig.txt");
            if (!File.Exists(filename))
                filename = Path.Combine(binFolder, "GameConfig.txt");

            var gameInfos = new List<GameConfiguration>();

            CompilePalLogger.LogLineDebug($"Reading Game Config: {filename}");
            using (var gameConfigFile = File.OpenRead(filename))
            {
                var data = KVSerializer.Deserialize(gameConfigFile);

                foreach (var gamedb in (IEnumerable<KVObject>)data["Games"])
                {
                    try
                    {
                        var hdb = gamedb["Hammer"];
                        if (hdb is null)
                        {
                            CompilePalLogger.LogLineDebug($"GameInfo block is missing Hammer section: {gamedb}");
                            continue;
                        }

                        CompilePalLogger.LogLineDebug($"Gamedb: {gamedb}");

                        // use vbsp as a backup path for finding other compile executables if they are in a non standard location
                        var vbsp = GetFullPath((hdb["BSP"] ?? hdb["bsp"]).ToString(), binFolder);

                        var bspzip = ToolsPlusPlusPaths.FindCompanion("bspzip", binFolder, vbsp) ?? string.Empty;
                        var vbspinfo = ToolsPlusPlusPaths.FindCompanion("vbspinfo", binFolder, vbsp) ?? string.Empty;
                        var vpk = ToolsPlusPlusPaths.FindCompanion("vpk", binFolder, vbsp) ?? string.Empty;


                        GameConfiguration game = new GameConfiguration
                        {
                            Name = gamedb.Name.Replace("\"", ""),
                            BinFolder = binFolder,
                            GameFolder = GetFullPath((gamedb["GameDir"] ?? gamedb["gamedir"]).ToString(), binFolder),
                            GameEXE = GetFullPath((hdb["GameExe"] ?? hdb["gamexe"]).ToString(), binFolder),
                            SDKMapFolder = GetFullPath((hdb["MapDir"] ?? hdb["mapdir"]).ToString(), binFolder),
                            VBSP = vbsp,
                            VVIS = GetFullPath((hdb["Vis"] ?? hdb["vis"]).ToString(), binFolder),
                            VRAD = GetFullPath((hdb["Light"] ?? hdb["light"]).ToString(), binFolder),
                            MapFolder = GetFullPath((hdb["BSPDir"] ?? hdb["bspdir"]).ToString(), binFolder),
                            BSPZip = bspzip,
                            VBSPInfo = vbspinfo,
                            VPK = vpk,
                        };

                        var cpdb = gamedb["CompilePal"];
                        if (cpdb is not null)
                        {
                            CompilePalLogger.LogLineDebug($"Found CompilePal GameInfo block");
                            var pluginFolder = cpdb["Plugins"].ToString();
                            if (!string.IsNullOrEmpty(pluginFolder))
                            {
                                game.PluginFolder = pluginFolder;
                            }
                        }

                        game.SteamAppID = GetSteamAppID(game);
                        gameInfos.Add(game);
                    }
                    catch (Exception ex)
                    {
                        CompilePalLogger.LogLine($"Failed to parse game configuration: {ex}");
                    }
                }
            }

            return gameInfos;
        }

        private static string GetFullPath(string line, string gameInfoDir)
        {
            return ToolsPlusPlusPaths.Resolve(line, gameInfoDir);
        }

        private static int? GetSteamAppID(GameConfiguration config)
        {
            if (!File.Exists(config.GameInfoPath)) return null;

            using (var gameInfoFile = File.OpenRead(config.GameInfoPath))
            {
                var gameInfo = KVSerializer.Deserialize(gameInfoFile);
                var appIDValue = gameInfo["FileSystem"]?["SteamAppId"];
                if (appIDValue is null)
                    return null;

                Int32.TryParse(appIDValue.ToString(), out int appID);
                return appID;
            }
        }
    }
}
