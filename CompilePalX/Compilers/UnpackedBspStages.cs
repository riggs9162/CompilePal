using System;
using System.IO;
using System.Threading;
using CompilePalX.Compilers.BSPPack;
using CompilePalX.Compiling;

namespace CompilePalX.Compilers
{
    /// <summary>
    /// Runs a stage that reads the BSP beside the VMF, first swapping a compressed BSP for its uncompressed
    /// .bsp.unpacked backup. A compile that runs VBSP has a fresh uncompressed BSP by now, so nothing is restored.
    /// </summary>
    class UnpackedBspStage(string metadata, string? parameterFolder = null) : CompileExecutable(metadata, parameterFolder)
    {
        public override void Run(CompileContext c, CancellationToken cancellationToken)
        {
            if (CanRun(c))
                RestoreUnpackedMap(c);

            base.Run(c, cancellationToken);
        }

        private static void RestoreUnpackedMap(CompileContext context)
        {
            string unpackedFile = context.BSPFile + ".unpacked";

            try
            {
                if (!File.Exists(context.BSPFile) || !File.Exists(unpackedFile) || !BSP.IsCompressed(context.BSPFile))
                    return;

                CompilePalLogger.LogLine($"Restoring uncompressed bsp from .unpacked backup dated {File.GetLastWriteTime(unpackedFile):yyyy-MM-dd HH:mm}...");
                CompilePalLogger.LogLineDebug($"Copying {unpackedFile} to {context.BSPFile}");
                File.Copy(unpackedFile, context.BSPFile, true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                CompilePalLogger.LogCompileError($"Could not restore {context.BSPFile} from its .unpacked backup: {exception.Message}\n",
                    new Error($"Could not restore {context.BSPFile} from its .unpacked backup", ErrorSeverity.Error));
            }
        }
    }

    /// <summary>
    /// Runs REPACK on the BSP in the maps folder, then gives the VMF folder the same compressed BSP.
    /// </summary>
    class RepackProcess(string metadata, string? parameterFolder = null) : CompileExecutable(metadata, parameterFolder)
    {
        public override void Run(CompileContext c, CancellationToken cancellationToken)
        {
            bool syncVmfFolder = CanRun(c) && IsMapCopyCurrent(c);

            base.Run(c, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (syncVmfFolder)
                CopyRepackedMapToVmfFolder(c);
        }

        /// <summary>
        /// Checks whether the maps folder holds this compile's BSP, separately from the one beside the VMF.
        /// A copy older than the VMF folder's BSP is left over from an earlier compile.
        /// </summary>
        private static bool IsMapCopyCurrent(CompileContext context)
        {
            return File.Exists(context.BSPFile)
                && File.Exists(context.CopyLocation)
                && !Path.GetFullPath(context.CopyLocation).Equals(Path.GetFullPath(context.BSPFile), StringComparison.OrdinalIgnoreCase)
                && File.GetLastWriteTimeUtc(context.CopyLocation) >= File.GetLastWriteTimeUtc(context.BSPFile);
        }

        /// <summary>
        /// Replaces the BSP beside the VMF with the compressed BSP from the maps folder.
        /// The uncompressed BSP stays available as .bsp.unpacked, and a backup PACK made during
        /// this compile is kept because it has no packed assets in it.
        /// A failed copy is reported without stopping the stages that follow.
        /// </summary>
        private static void CopyRepackedMapToVmfFolder(CompileContext context)
        {
            try
            {
                if (!context.HasUnpackedBackup && !BSP.IsCompressed(context.BSPFile))
                {
                    CompilePalLogger.LogLineDebug($"Copying {context.BSPFile} to {context.BSPFile}.unpacked");
                    File.Copy(context.BSPFile, context.BSPFile + ".unpacked", true);
                }

                CompilePalLogger.LogLine("Copying repacked bsp to vmf folder...");
                CompilePalLogger.LogLineDebug($"Copying {context.CopyLocation} to {context.BSPFile}");
                File.Copy(context.CopyLocation, context.BSPFile, true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                CompilePalLogger.LogCompileError($"Could not copy repacked bsp to {context.BSPFile}: {exception.Message}\n",
                    new Error($"Could not copy repacked bsp to {context.BSPFile}", ErrorSeverity.Error));
            }
        }
    }
}
