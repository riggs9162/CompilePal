using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CompilePalX.Compilers.BSPPack
{
    /// <summary>
    /// Reads the file index of a Valve VPK archive and extracts single files from it.
    /// Version 1 and version 2 archives are supported, which covers every Source 2013 era game.
    /// </summary>
    class VpkArchive
    {
        private const uint Signature = 0x55AA1234;
        private const ushort EmbeddedArchiveIndex = 0x7FFF;
        private const ushort EntryTerminator = 0xFFFF;
        private const int EntryHeaderSize = 18;
        private const string DirectorySuffix = "_dir.vpk";

        /// <summary>
        /// Location of one file inside the archive.
        /// </summary>
        /// <param name="ArchiveIndex">Number of the numbered archive part that holds the data, or <see cref="EmbeddedArchiveIndex"/> for the index file itself.</param>
        /// <param name="Offset">Byte offset of the data inside that archive part.</param>
        /// <param name="Length">Number of data bytes stored in that archive part.</param>
        /// <param name="Preload">Leading bytes of the file that are stored inside the index.</param>
        private readonly record struct Entry(ushort ArchiveIndex, uint Offset, uint Length, byte[] Preload);

        private readonly Dictionary<string, Dictionary<string, Entry>> folders = new(StringComparer.OrdinalIgnoreCase);
        private readonly string archivePartPrefix;
        private long embeddedDataOffset;

        /// <summary>
        /// Full path of the <c>_dir.vpk</c> index file.
        /// </summary>
        public string IndexPath { get; }

        /// <summary>
        /// Number of files listed in the archive.
        /// </summary>
        public int FileCount { get; private set; }

        private VpkArchive(string indexPath)
        {
            IndexPath = indexPath;
            archivePartPrefix = indexPath.Substring(0, indexPath.Length - DirectorySuffix.Length);
        }

        /// <summary>
        /// Reads the file index of an archive.
        /// </summary>
        /// <param name="indexPath">Path to the archive's <c>_dir.vpk</c> file.</param>
        /// <returns>The archive.</returns>
        /// <exception cref="InvalidDataException">The file is not a supported VPK index.</exception>
        /// <exception cref="IOException">The file could not be read.</exception>
        public static VpkArchive Open(string indexPath)
        {
            if (!indexPath.EndsWith(DirectorySuffix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"{indexPath} is not a VPK index file");

            var archive = new VpkArchive(Path.GetFullPath(indexPath));
            archive.ReadIndex();
            return archive;
        }

        /// <summary>
        /// Checks whether the archive lists a file.
        /// </summary>
        /// <param name="internalPath">Game-relative path, such as <c>materials/decals/stain.vmt</c>.</param>
        /// <returns>True when the archive holds the file.</returns>
        public bool Contains(string internalPath)
        {
            return TryGetEntry(internalPath, out _);
        }

        /// <summary>
        /// Writes one file from the archive to disk.
        /// </summary>
        /// <param name="internalPath">Game-relative path, such as <c>materials/decals/stain.vmt</c>.</param>
        /// <param name="destinationPath">File to create. Missing parent folders are created.</param>
        /// <returns>True when the file was written, false when the archive does not hold it.</returns>
        /// <exception cref="IOException">The archive part or the destination could not be accessed.</exception>
        public bool TryExtract(string internalPath, string destinationPath)
        {
            if (!TryGetEntry(internalPath, out var entry))
                return false;

            var data = new byte[entry.Preload.Length + entry.Length];
            entry.Preload.CopyTo(data, 0);

            if (entry.Length > 0)
            {
                bool embedded = entry.ArchiveIndex == EmbeddedArchiveIndex;
                string partPath = embedded ? IndexPath : $"{archivePartPrefix}_{entry.ArchiveIndex:D3}.vpk";
                long start = embedded ? embeddedDataOffset + entry.Offset : entry.Offset;

                using var part = File.OpenRead(partPath);
                part.Seek(start, SeekOrigin.Begin);
                part.ReadExactly(data, entry.Preload.Length, (int)entry.Length);
            }

            string? folder = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            File.WriteAllBytes(destinationPath, data);
            return true;
        }

        /// <summary>
        /// Turns a game-relative path into the form the index uses: forward slashes,
        /// no leading slash, and <c>.</c> or <c>..</c> segments resolved.
        /// </summary>
        /// <param name="internalPath">Game-relative path.</param>
        /// <returns>The normalized path, or an empty string when nothing is left.</returns>
        public static string NormalizePath(string internalPath)
        {
            var segments = new List<string>();
            foreach (string segment in internalPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment == ".")
                    continue;

                if (segment == "..")
                {
                    if (segments.Count > 0)
                        segments.RemoveAt(segments.Count - 1);
                    continue;
                }

                segments.Add(segment);
            }

            return string.Join('/', segments);
        }

        private bool TryGetEntry(string internalPath, out Entry entry)
        {
            entry = default;
            string path = NormalizePath(internalPath);
            int split = path.LastIndexOf('/');
            string folder = split < 0 ? "" : path.Substring(0, split);
            string file = path.Substring(split + 1);

            return folders.TryGetValue(folder, out var files) && files.TryGetValue(file, out entry);
        }

        private void ReadIndex()
        {
            using var stream = File.OpenRead(IndexPath);
            using var reader = new BinaryReader(stream);

            if (reader.ReadUInt32() != Signature)
                throw new InvalidDataException($"{IndexPath} is not a VPK archive");

            uint version = reader.ReadUInt32();
            uint treeSize = reader.ReadUInt32();
            int headerSize = version switch
            {
                1 => 12,
                2 => 28,
                _ => throw new InvalidDataException($"{IndexPath} uses unsupported VPK version {version}"),
            };

            stream.Seek(headerSize, SeekOrigin.Begin);
            byte[] tree = reader.ReadBytes((int)treeSize);
            if (tree.Length != treeSize)
                throw new InvalidDataException($"{IndexPath} has a truncated file index");

            embeddedDataOffset = headerSize + treeSize;
            ParseTree(tree);
        }

        private void ParseTree(byte[] tree)
        {
            int position = 0;
            while (true)
            {
                string extension = ReadString(tree, ref position);
                if (extension.Length == 0)
                    break;

                while (true)
                {
                    string folder = ReadString(tree, ref position);
                    if (folder.Length == 0)
                        break;

                    string folderKey = NormalizePath(folder.Trim());
                    if (!folders.TryGetValue(folderKey, out var files))
                    {
                        files = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
                        folders.Add(folderKey, files);
                    }

                    while (true)
                    {
                        string name = ReadString(tree, ref position);
                        if (name.Length == 0)
                            break;

                        files[FileKey(name, extension)] = ReadEntry(tree, ref position);
                        FileCount++;
                    }
                }
            }
        }

        private Entry ReadEntry(byte[] tree, ref int position)
        {
            if (position + EntryHeaderSize > tree.Length)
                throw new InvalidDataException($"{IndexPath} has a truncated file entry");

            var header = tree.AsSpan(position, EntryHeaderSize);
            ushort preloadSize = BitConverter.ToUInt16(header.Slice(4, 2));
            ushort archiveIndex = BitConverter.ToUInt16(header.Slice(6, 2));
            uint offset = BitConverter.ToUInt32(header.Slice(8, 4));
            uint length = BitConverter.ToUInt32(header.Slice(12, 4));
            ushort terminator = BitConverter.ToUInt16(header.Slice(16, 2));
            position += EntryHeaderSize;

            if (terminator != EntryTerminator || position + preloadSize > tree.Length)
                throw new InvalidDataException($"{IndexPath} has a damaged file entry");

            byte[] preload = preloadSize == 0 ? Array.Empty<byte>() : tree.AsSpan(position, preloadSize).ToArray();
            position += preloadSize;

            return new Entry(archiveIndex, offset, length, preload);
        }

        private static string FileKey(string name, string extension)
        {
            return extension.Trim().Length == 0 ? name : $"{name}.{extension}";
        }

        private string ReadString(byte[] tree, ref int position)
        {
            int end = Array.IndexOf(tree, (byte)0, position);
            if (end < 0)
                throw new InvalidDataException($"{IndexPath} has a truncated file index");

            string value = Encoding.UTF8.GetString(tree, position, end - position);
            position = end + 1;
            return value;
        }
    }
}
