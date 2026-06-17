using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AxiomateInstaller.Services;

/// <summary>
/// Extracts the embedded Git installer, Python installer, Uninstaller.exe and
/// dist/ payload to a working directory under %TEMP%. Returns absolute paths
/// to the extracted top-level files / dist folder.
/// </summary>
public sealed class PayloadExtractor
{
    private readonly Logger _log;
    private readonly Assembly _asm;

    public PayloadExtractor(Logger log)
    {
        _log = log;
        _asm = typeof(PayloadExtractor).Assembly;
    }

    public sealed class ExtractedPayload
    {
        public required string WorkDir { get; init; }
        public required string GitInstaller { get; init; }
        public required string PythonInstaller { get; init; }
        public required string UninstallerExe { get; init; }
        public required string DistDir { get; init; }
    }

    public ExtractedPayload Extract()
    {
        string root = Path.Combine(Path.GetTempPath(), "axiomate-installer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string distDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(distDir);

        _log.Info($"Extracting embedded payload to {root}");

        // Embedded resource names look like:
        //   AxiomateInstaller.Resources.Git-2.54.0-64-bit.exe
        //   AxiomateInstaller.Resources.python-3.12.10-amd64.exe
        //   AxiomateInstaller.Resources.Uninstaller.exe
        //   AxiomateInstaller.Resources.dist.<filename>            (flat dist files)
        //   AxiomateInstaller.Resources.dist.<subdir>.<filename>   (rare)
        string asmName = _asm.GetName().Name ?? "AxiomateInstaller";
        string prefix = asmName + ".Resources.";
        string distPrefix = prefix + "dist.";

        string? gitPath = null, pyPath = null, uninstPath = null;
        int distCount = 0;
        long distBytes = 0;

        foreach (var resName in _asm.GetManifestResourceNames())
        {
            if (!resName.StartsWith(prefix, StringComparison.Ordinal)) continue;

            if (resName.StartsWith(distPrefix, StringComparison.Ordinal))
            {
                string rel = resName[distPrefix.Length..];
                // Only the LAST dot is the extension. We accept that build.ps1 syncs
                // a flat dist (which is the case for the upstream agent build).
                string outFile = Path.Combine(distDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
                long n = WriteResource(resName, outFile);
                distCount++;
                distBytes += n;
                continue;
            }

            string baseName = resName[prefix.Length..];
            string outPath = Path.Combine(root, baseName);
            if (baseName.StartsWith("Git-", StringComparison.OrdinalIgnoreCase))     gitPath = outPath;
            else if (baseName.StartsWith("python-", StringComparison.OrdinalIgnoreCase)) pyPath = outPath;
            else if (string.Equals(baseName, "Uninstaller.exe", StringComparison.OrdinalIgnoreCase)) uninstPath = outPath;

            WriteResource(resName, outPath);
        }

        if (gitPath is null) throw new InvalidOperationException("Git installer payload missing from embedded resources.");
        if (pyPath  is null) throw new InvalidOperationException("Python installer payload missing from embedded resources.");
        if (uninstPath is null) throw new InvalidOperationException("Uninstaller payload missing from embedded resources.");
        if (distCount == 0) throw new InvalidOperationException("Axiomate dist payload missing from embedded resources. Run build.ps1 first.");

        _log.Info($"Extracted dist: {distCount} files, {distBytes:N0} bytes");
        return new ExtractedPayload
        {
            WorkDir = root,
            GitInstaller = gitPath,
            PythonInstaller = pyPath,
            UninstallerExe = uninstPath,
            DistDir = distDir
        };
    }

    private long WriteResource(string resName, string outPath)
    {
        using Stream? src = _asm.GetManifestResourceStream(resName)
            ?? throw new InvalidOperationException($"Embedded resource missing: {resName}");
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        using var dst = File.Create(outPath);
        src.CopyTo(dst);
        return src.Length;
    }

    public static void Cleanup(string workDir, Logger log)
    {
        try
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, recursive: true);
                log.Info($"Cleaned up temp dir: {workDir}");
            }
        }
        catch (Exception ex)
        {
            log.Warn($"Could not delete temp dir {workDir}: {ex.Message}");
        }
    }
}
