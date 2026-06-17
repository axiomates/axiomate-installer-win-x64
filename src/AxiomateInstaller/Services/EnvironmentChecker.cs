using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AxiomateInstaller.Services;

public sealed record EnvCheckResult(
    bool IsSupportedOs,
    string OsDisplay,
    bool IsX64,
    string ArchDisplay,
    bool GitOk,
    string? GitVersionDisplay,
    bool PythonOk,
    string? PythonVersionDisplay)
{
    public bool MeetsBaselineRequirements => IsSupportedOs && IsX64;
}

/// <summary>
/// Detects whether the current machine meets baseline requirements
/// (Windows 11+, x64) and whether Git/Python are present at acceptable versions.
/// </summary>
public sealed class EnvironmentChecker
{
    public const int MinWin11Build  = 22000;
    public const int MinGitMajor    = 2;
    public const int MinGitMinor    = 40;
    public const int MinPyMajor     = 3;
    public const int MinPyMinor     = 12;

    private readonly Logger _log;
    public EnvironmentChecker(Logger log) { _log = log; }

    public async Task<EnvCheckResult> RunAsync()
    {
        var (osOk, osDisplay) = CheckOs();
        var (archOk, archDisplay) = CheckArch();
        var (gitOk, gitDisp) = await CheckGitAsync();
        var (pyOk, pyDisp)   = await CheckPythonAsync();
        return new EnvCheckResult(osOk, osDisplay, archOk, archDisplay, gitOk, gitDisp, pyOk, pyDisp);
    }

    private (bool, string) CheckOs()
    {
        var v = Environment.OSVersion.Version;
        bool ok = v.Major >= 10 && v.Build >= MinWin11Build;
        string display = $"Windows {v.Major}.{v.Minor} (build {v.Build})";
        _log.Info($"OS check: {display} -> {(ok ? "OK" : $"FAIL (need Windows 11 build {MinWin11Build}+)")}");
        return (ok, display);
    }

    private (bool, string) CheckArch()
    {
        var arch = RuntimeInformation.OSArchitecture;
        bool ok = arch == Architecture.X64;
        _log.Info($"Arch check: {arch} -> {(ok ? "OK" : "FAIL (need x64)")}");
        return (ok, arch.ToString());
    }

    private async Task<(bool, string?)> CheckGitAsync()
    {
        try
        {
            string? raw = await RunCaptureAsync("git", "--version", timeoutMs: 5000);
            if (string.IsNullOrWhiteSpace(raw)) { _log.Info("Git check: not found"); return (false, null); }
            var m = Regex.Match(raw, @"git version (\d+)\.(\d+)(?:\.(\d+))?");
            if (!m.Success) { _log.Info($"Git check: unparseable -> {raw.Trim()}"); return (false, raw.Trim()); }
            int major = int.Parse(m.Groups[1].Value);
            int minor = int.Parse(m.Groups[2].Value);
            bool ok = major > MinGitMajor || (major == MinGitMajor && minor >= MinGitMinor);
            string disp = $"git {major}.{minor}" + (m.Groups[3].Success ? $".{m.Groups[3].Value}" : "");
            _log.Info($"Git check: {disp} -> {(ok ? "OK" : $"too old (need >= {MinGitMajor}.{MinGitMinor})")}");
            return (ok, disp);
        }
        catch (Exception ex)
        {
            _log.Info($"Git check: probe error -> {ex.Message}");
            return (false, null);
        }
    }

    private async Task<(bool, string?)> CheckPythonAsync()
    {
        // Try py -3.12, then python, then python3.
        string?[] probes = { "py:-3.12 --version", "python:--version", "python3:--version" };
        foreach (var probe in probes)
        {
            if (probe is null) continue;
            int idx = probe.IndexOf(':');
            string exe  = probe[..idx];
            string args = probe[(idx + 1)..];
            string? raw = await RunCaptureAsync(exe, args, timeoutMs: 5000);
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var m = Regex.Match(raw, @"Python (\d+)\.(\d+)\.(\d+)");
            if (!m.Success) continue;
            int major = int.Parse(m.Groups[1].Value);
            int minor = int.Parse(m.Groups[2].Value);
            int patch = int.Parse(m.Groups[3].Value);
            bool ok = major > MinPyMajor || (major == MinPyMajor && minor >= MinPyMinor);
            string disp = $"Python {major}.{minor}.{patch} (via {exe} {args})";
            _log.Info($"Python check: {disp} -> {(ok ? "OK" : $"too old (need >= {MinPyMajor}.{MinPyMinor})")}");
            return (ok, disp);
        }
        _log.Info("Python check: not found");
        return (false, null);
    }

    private static async Task<string?> RunCaptureAsync(string exe, string args, int timeoutMs)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null) return null;
            string stdout = await p.StandardOutput.ReadToEndAsync();
            string stderr = await p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(true); } catch { }
                return null;
            }
            // Some tools print version to stderr (older python).
            return string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
        }
        catch (Win32Exception)  { return null; } // executable not found
        catch (FileNotFoundException) { return null; }
        catch (Exception) { return null; }
    }
}
