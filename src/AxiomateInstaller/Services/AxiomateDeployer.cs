using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AxiomateInstaller.Services;

/// <summary>
/// Wipes the install dir (user has already confirmed) and copies the extracted
/// dist folder into it. Records a per-file manifest so the uninstaller can
/// remove exactly what was placed.
/// </summary>
public sealed class AxiomateDeployer
{
    private readonly Logger _log;
    public AxiomateDeployer(Logger log) { _log = log; }

    public async Task<DeployManifest> DeployAsync(string distDir, string installDir, string axiomateVersion, string installerVersion)
    {
        if (!Directory.Exists(distDir))
            throw new InstallStepException(Strings.Format("Err_Distmissing_Format", distDir));

        var installEval = DirGuard.Evaluate(installDir);
        if (!installEval.Ok) throw new InstallStepException(installEval.Reason ?? Strings.Get("Path_BadTitle"));

        // Force clean the install dir.
        await Task.Run(() =>
        {
            if (Directory.Exists(installDir))
            {
                // A still-running Axiomate (or its helpers) holds open handles to
                // its own image under installDir, which makes the delete below fail
                // with "Access to the path 'axiomate.exe' is denied". Terminate
                // those first (we already hold the elevation needed to do so).
                new RunningProcessGuard(_log).TerminateProcessesUnder(installDir);
                _log.Info($"Cleaning install dir {installDir}");
                ForceDeleteDirectory(installDir);
            }
            Directory.CreateDirectory(installDir);
        });

        var files = new List<string>();
        long totalBytes = 0;
        await Task.Run(() =>
        {
            foreach (string srcFile in Directory.EnumerateFiles(distDir, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(distDir, srcFile);
                string dst = Path.Combine(installDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(srcFile, dst, overwrite: true);
                files.Add(rel.Replace('/', '\\'));
                totalBytes += new FileInfo(dst).Length;
            }
        });

        _log.Info($"Deployed {files.Count} files ({totalBytes:N0} bytes) to {installDir}");

        var manifest = new DeployManifest
        {
            InstallerVersion = installerVersion,
            AxiomateVersion = axiomateVersion,
            InstallDir = installDir,
            InstalledAtUtc = DateTime.UtcNow,
            TotalBytes = totalBytes,
            Files = files
        };

        string manifestPath = Path.Combine(installDir, "installation-manifest.json");
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        _log.Info($"Wrote manifest: {manifestPath}");

        return manifest;
    }

    private static void ForceDeleteDirectory(string dir)
    {
        // Clear read-only attributes that some payload files (e.g. tools shipped
        // from another machine) might have inherited.
        foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
        }
        Directory.Delete(dir, recursive: true);
    }
}

public sealed class DeployManifest
{
    public string InstallerVersion { get; set; } = "";
    public string AxiomateVersion  { get; set; } = "";
    public string InstallDir       { get; set; } = "";
    public DateTime InstalledAtUtc { get; set; }
    public long TotalBytes         { get; set; }
    public List<string> Files      { get; set; } = new();
}
