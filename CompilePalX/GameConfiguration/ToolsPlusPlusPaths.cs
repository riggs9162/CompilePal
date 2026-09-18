using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CompilePalX;

/// <summary>Resolves compiler paths without changing the game's bin directory.</summary>
internal static class ToolsPlusPlusPaths
{
    private static readonly string[] SuiteNames = { "vbsp", "vvis", "vrad", "bspzip" };

    public static string Clean(string? value)
    {
        string path = value?.Trim() ?? string.Empty;
        if (path.Length >= 2 && path[0] == '"' && path[^1] == '"')
            path = path[1..^1];
        return Environment.ExpandEnvironmentVariables(path);
    }

    public static string Resolve(string? value, string? baseDirectory)
    {
        string path = Clean(value);
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);
        string root = Clean(baseDirectory);
        return Path.GetFullPath(Path.Combine(string.IsNullOrEmpty(root) ? "." : root, path));
    }

    public static bool IsPlusPlus(string? executable, string toolName)
    {
        if (string.IsNullOrWhiteSpace(executable))
            return false;
        return string.Equals(Path.GetFileName(Clean(executable)), toolName + "++.exe",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Prefer the standalone packer next to an explicitly selected VBSP++.
    /// Stock configurations keep their original stock-tool preference.
    /// Optional utilities may be absent; this never changes the caller's bin folder.
    /// </summary>
    public static string? FindCompanion(string name, string? gameBin, string? referenceExecutable)
    {
        string root = Resolve(gameBin, ".");
        string reference = Resolve(referenceExecutable, string.IsNullOrEmpty(root) ? "." : root);
        string? adjacent = string.IsNullOrEmpty(reference) ? null : Path.GetDirectoryName(reference);
        var candidates = new List<string>();
        bool hasPlusPlusVariant = SuiteNames.Contains(name, StringComparer.OrdinalIgnoreCase);

        void Add(string? directory, string filename)
        {
            if (!string.IsNullOrWhiteSpace(directory))
                candidates.Add(Path.Combine(directory, filename));
        }

        bool preferPlusPlus = hasPlusPlusVariant && IsPlusPlus(reference, "vbsp");
        if (preferPlusPlus)
        {
            Add(adjacent, name + "++.exe");
            Add(root, name + "++.exe");
        }
        Add(root, name + ".exe");
        Add(adjacent, name + ".exe");
        if (hasPlusPlusVariant && !preferPlusPlus)
        {
            Add(root, name + "++.exe");
            Add(adjacent, name + "++.exe");
        }
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(File.Exists);
    }

    /// <summary>Validates all four files before the caller changes any configuration.</summary>
    public static bool TrySelectSuite(string? directory, out Dictionary<string, string> paths,
        out string error)
    {
        paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        error = string.Empty;
        try
        {
            string root = Resolve(directory, ".");
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                error = "Select the extracted Tools++ directory.";
                return false;
            }
            var selected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var missing = new List<string>();
            foreach (string name in SuiteNames)
            {
                string filename = name + "++.exe";
                string path = Path.Combine(root, filename);
                if (!File.Exists(path))
                    missing.Add(filename);
                else
                    selected.Add(name, path);
            }
            if (missing.Count != 0)
            {
                error = "The selected directory is missing: " + string.Join(", ", missing)
                    + ". No compiler paths were changed.";
                return false;
            }
            paths = selected;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            error = "Cannot use that directory: " + exception.Message;
            return false;
        }
    }

    /// <summary>Only default working directories are replaced; custom plugin overrides survive.</summary>
    public static string WorkingDirectory(string? executable, string? fallback, bool useDefault = true)
    {
        string original = string.IsNullOrWhiteSpace(fallback) ? "." : fallback;
        if (!useDefault || !SuiteNames.Any(name => IsPlusPlus(executable, name)))
            return original;
        string resolved = Resolve(executable, ".");
        return Path.GetDirectoryName(resolved) ?? original;
    }

    /// <summary>Uses an existing, unrenamed BSPZIP++ filename as a capability hint, not executable identity verification.</summary>
    public static bool SupportsGModRepack(string? executable) =>
        IsPlusPlus(executable, "bspzip") && File.Exists(Clean(executable));
}
