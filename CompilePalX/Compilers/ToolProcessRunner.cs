using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CompilePalX.Compiling;

namespace CompilePalX.Compilers;

/// <summary>Runs a utility with cancellable output reads and process-tree cancellation.</summary>
internal static class ToolProcessRunner
{
    internal static string Run(ProcessStartInfo startInfo, CancellationToken cancellationToken,
        Action<string>? log = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        using var registration = cancellationToken.Register(() => Stop(process));
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errors = process.StandardError.ReadToEndAsync(cancellationToken);
        process.WaitForExitAsync(cancellationToken).GetAwaiter().GetResult();
        string text = output.GetAwaiter().GetResult();
        string errorText = errors.GetAwaiter().GetResult();
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrEmpty(text))
            log?.Invoke(text);
        if (!string.IsNullOrEmpty(errorText))
            log?.Invoke(errorText);
        if (process.ExitCode != 0)
            throw new IOException($"{Path.GetFileName(startInfo.FileName)} exited with code {process.ExitCode} (0x{process.ExitCode:X}). {errorText}".Trim());
        return text;
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (Win32Exception exception)
        {
            CompilePalLogger.LogDebug($"Could not stop utility process: {exception.Message}");
        }
    }
}
