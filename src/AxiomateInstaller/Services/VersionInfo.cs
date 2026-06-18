using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AxiomateInstaller.Services;

/// <summary>
/// Reads version.json (embedded as content) at startup so the UI / log filenames
/// can show the installer version. Versions are baked into the assembly metadata
/// at build time as well; this class is mostly for human-readable display.
/// </summary>
public sealed class VersionInfo
{
    public string InstallerVersion { get; }
    public string AxiomateVersion { get; }
    public string BundledGitVersion { get; }
    public string BundledPythonVersion { get; }

    public VersionInfo(
        string installerVersion,
        string axiomateVersion,
        string bundledGitVersion,
        string bundledPythonVersion)
    {
        InstallerVersion = installerVersion;
        AxiomateVersion = axiomateVersion;
        BundledGitVersion = bundledGitVersion;
        BundledPythonVersion = bundledPythonVersion;
    }

    public static VersionInfo Load()
    {
        // Try assembly metadata first (set by build.ps1 via -p:InformationalVersion).
        string installer = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0";

        // Strip any "+axiomate.x.y.z" suffix to keep just the SemVer part.
        int plus = installer.IndexOf('+');
        string installerSemver = plus > 0 ? installer[..plus] : installer;
        string axiomateFromMeta = plus > 0 && installer.Length > plus + 10
            ? CleanVersion(installer[(plus + "+axiomate.".Length)..])
            : "unknown";

        // Fallback: try version.json next to the EXE in dev builds.
        string git = "2.54.0";
        string py  = "3.12.10";
        string axiomate = axiomateFromMeta;
        try
        {
            string exeDir = AppContext.BaseDirectory;
            string vjson  = Path.Combine(exeDir, "version.json");
            if (File.Exists(vjson))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(vjson));
                var root = doc.RootElement;
                if (root.TryGetProperty("axiomateVersion", out var av) && av.ValueKind == JsonValueKind.String)
                {
                    string s = av.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(s) && s != "auto") axiomate = CleanVersion(s);
                }
                if (root.TryGetProperty("bundledGitVersion", out var gv) && gv.ValueKind == JsonValueKind.String)
                    git = gv.GetString() ?? git;
                if (root.TryGetProperty("bundledPythonVersion", out var pv) && pv.ValueKind == JsonValueKind.String)
                    py = pv.GetString() ?? py;
            }
        }
        catch { /* fallback values are fine */ }

        return new VersionInfo(installerSemver, axiomate, git, py);
    }

    private static string CleanVersion(string raw)
    {
        var m = Regex.Match(raw, @"^(\d+\.\d+\.\d+\.\d+)");
        return m.Success ? m.Groups[1].Value : raw;
    }
}
