using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CompilePalX.Compiling;
using ValveKeyValue;

namespace CompilePalX.Compilers.BSPPack
{
    /// <summary>
    /// Finds assets that only exist inside the VPK archives of games mounted into the current game,
    /// and unpacks them into a folder so they can be packed like loose files.
    /// Assets that the game itself ships in its own archives are never unpacked, because every player already has them.
    /// Archives are read the first time an asset is looked up, so maps that only use loose content never touch them.
    /// </summary>
    class MountedArchiveContent
    {
        private static readonly KVSerializer KVSerializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);

        private readonly IReadOnlyList<string> gameArchivePaths;
        private readonly IReadOnlyList<string> mountedArchivePaths;
        private readonly Dictionary<string, string> lookups = new(StringComparer.OrdinalIgnoreCase);
        private List<VpkArchive>? gameArchives;
        private List<VpkArchive>? mountedArchives;

        /// <summary>
        /// Folder that unpacked assets are written to, laid out like a game folder.
        /// </summary>
        public string ExtractionFolder { get; }

        /// <summary>
        /// Number of assets unpacked so far.
        /// </summary>
        public int ExtractedFileCount { get; private set; }

        /// <summary>
        /// Number of mounted game archives that will be searched.
        /// </summary>
        public int MountedArchiveCount => mountedArchivePaths.Count;

        /// <param name="gameArchivePaths"><c>_dir.vpk</c> files that belong to the game itself. Their assets are never packed.</param>
        /// <param name="mountedArchivePaths"><c>_dir.vpk</c> files of mounted games, in search order.</param>
        /// <param name="extractionFolder">Folder to unpack assets into. It is emptied first.</param>
        public MountedArchiveContent(IEnumerable<string> gameArchivePaths, IEnumerable<string> mountedArchivePaths, string extractionFolder)
        {
            this.gameArchivePaths = gameArchivePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            this.mountedArchivePaths = mountedArchivePaths
                .Where(path => !this.gameArchivePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            ExtractionFolder = Path.GetFullPath(extractionFolder);

            if (Directory.Exists(ExtractionFolder))
                Directory.Delete(ExtractionFolder, true);
        }

        /// <summary>
        /// Unpacks an asset from the first mounted archive that holds it.
        /// </summary>
        /// <param name="internalPath">Game-relative path, such as <c>materials/decals/stain.vmt</c>.</param>
        /// <returns>
        /// Path of the unpacked file, or an empty string when the asset is not in a mounted archive or ships with the game.
        /// The part after <see cref="ExtractionFolder"/> keeps forward slashes, matching how loose files are reported.
        /// </returns>
        public string FindFile(string internalPath)
        {
            string path = VpkArchive.NormalizePath(internalPath);
            if (path.Length == 0 || mountedArchivePaths.Count == 0)
                return "";

            if (lookups.TryGetValue(path, out var cached))
                return cached;

            string result = Extract(path);
            lookups[path] = result;
            return result;
        }

        /// <summary>
        /// Finds the VPK archives that ship with the game itself: the ones gameinfo.txt lists,
        /// and any archive placed directly in the game folder. Their assets never need packing.
        /// </summary>
        /// <param name="gamePath">Game folder that holds gameinfo.txt</param>
        /// <returns>Full paths of the archives' <c>_dir.vpk</c> files</returns>
        public static List<string> FindGameArchives(string gamePath)
        {
            var archives = new List<string>();
            string gameInfoPath = Path.Combine(gamePath, "gameinfo.txt");
            string rootPath = Directory.GetParent(gamePath)!.ToString();

            if (File.Exists(gameInfoPath))
            {
                using var gameInfoFile = File.OpenRead(gameInfoPath);
                var gameInfo = KVSerializer.Deserialize(gameInfoFile);

                if (gameInfo?["FileSystem"]?["SearchPaths"] is IEnumerable<KVObject> searchPaths)
                {
                    foreach (var searchPathObject in searchPaths)
                    {
                        string? archivePath = ResolveArchiveSearchPath(searchPathObject.Value.ToString(), gamePath, rootPath);
                        if (archivePath is not null && File.Exists(archivePath))
                            archives.Add(archivePath);
                    }
                }
            }

            archives.AddRange(FindArchiveIndexes(gamePath));
            return archives.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Turns a gameinfo.txt search path that names a VPK archive into the path of its <c>_dir.vpk</c> file.
        /// </summary>
        /// <param name="searchPath">Search path value, such as <c>|all_source_engine_paths|hl2/hl2_textures.vpk</c></param>
        /// <param name="gamePath">Game folder that holds gameinfo.txt</param>
        /// <param name="rootPath">Folder that holds the game folder</param>
        /// <returns>The index file path, or null when the search path is not an archive or uses an unknown placeholder</returns>
        private static string? ResolveArchiveSearchPath(string? searchPath, string gamePath, string rootPath)
        {
            if (searchPath is null || !searchPath.EndsWith(".vpk", StringComparison.OrdinalIgnoreCase))
                return null;

            string path = searchPath
                .Replace("|all_source_engine_paths|", rootPath + "\\", StringComparison.OrdinalIgnoreCase)
                .Replace("|gameinfo_path|", gamePath + "\\", StringComparison.OrdinalIgnoreCase);
            if (path.Contains('|'))
                return null;

            if (!Path.IsPathRooted(path))
                path = Path.Combine(rootPath, path);

            return Path.GetFullPath(path.Substring(0, path.Length - ".vpk".Length) + "_dir.vpk");
        }

        /// <summary>
        /// Lists the VPK archives placed directly in a folder, sorted by name.
        /// </summary>
        /// <param name="folder">Folder to look in. Subfolders are not searched.</param>
        /// <returns>Full paths of the archives' <c>_dir.vpk</c> files</returns>
        public static IEnumerable<string> FindArchiveIndexes(string folder)
        {
            if (!Directory.Exists(folder))
                return [];

            return Directory.GetFiles(folder, "*_dir.vpk", SearchOption.TopDirectoryOnly)
                .Where(path => path.EndsWith("_dir.vpk", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFullPath)
                .Order(StringComparer.OrdinalIgnoreCase);
        }

        private string Extract(string path)
        {
            gameArchives ??= OpenArchives(gameArchivePaths);
            if (gameArchives.Any(archive => archive.Contains(path)))
                return "";

            mountedArchives ??= OpenArchives(mountedArchivePaths);
            foreach (var archive in mountedArchives)
            {
                if (!archive.Contains(path))
                    continue;

                string destination = Path.Combine(ExtractionFolder, path);
                try
                {
                    archive.TryExtract(path, destination);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    CompilePalLogger.LogLineDebug($"Failed to unpack {path} from {archive.IndexPath}: {e}");
                    CompilePalLogger.LogCompileError($"Could not unpack {path} from {archive.IndexPath}, skipping it\n",
                        new Error($"Could not unpack {path} from {archive.IndexPath}, skipping it", ErrorSeverity.Caution));
                    return "";
                }

                ExtractedFileCount++;
                CompilePalLogger.LogLineDebug($"Unpacked {path} from {archive.IndexPath}");
                return destination;
            }

            return "";
        }

        private static List<VpkArchive> OpenArchives(IEnumerable<string> paths)
        {
            var archives = new List<VpkArchive>();
            foreach (string path in paths)
            {
                try
                {
                    archives.Add(VpkArchive.Open(path));
                }
                catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    CompilePalLogger.LogLineDebug($"Failed to read archive {path}: {e}");
                    CompilePalLogger.LogCompileError($"Could not read archive {path}, skipping it\n",
                        new Error($"Could not read archive {path}, skipping it", ErrorSeverity.Caution));
                }
            }

            return archives;
        }
    }
}
