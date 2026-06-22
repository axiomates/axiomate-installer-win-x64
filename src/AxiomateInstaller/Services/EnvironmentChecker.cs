using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AxiomateInstaller.Services;

public sealed record EnvCheckResult(
    bool IsSupportedOs,
    bool IsWindows10,
    string OsDisplay,
    bool IsX64,
    string ArchDisplay,
    bool GitOk,
    string? GitVersionDisplay,
    bool PythonOk,
    string? PythonVersionDisplay,
    bool WindowsTerminalOk,
    string? WindowsTerminalDisplay)
{
    public bool NeedsWindowsTerminal => IsWindows10 && !WindowsTerminalOk;
    public bool MeetsBaselineRequirements => IsSupportedOs && IsX64 && !NeedsWindowsTerminal;
}

/// <summary>
/// Detects whether the current machine meets baseline requirements
/// (Windows 10+, x64) and whether Git/Python/Windows Terminal are present.
/// </summary>
public sealed class EnvironmentChecker
{
    public const int MinWin10Build  = 10240;
    public const int MinWin11Build  = 22000;
    public const int MinGitMajor    = 2;
    public const int MinGitMinor    = 40;
    public const int MinPyMajor     = 3;
    public const int MinPyMinor     = 12;

    private readonly Logger _log;
    private readonly string _searchPath;

    public EnvironmentChecker(Logger log)
    {
        _log = log;
        _searchPath = BuildSearchPath();
    }

    /// <summary>
    /// Build a PATH that merges machine + current user + the original (non-elevated)
    /// user's HKCU PATH. The installer runs elevated, so its inherited Path may not
    /// include user-installed Git / Python. We probe HKCU as well so per-user installs
    /// don't get falsely flagged as "not found".
    /// </summary>
    private string BuildSearchPath()
    {
        var parts = new List<string>();

        // 1. Process PATH (already merged HKLM + the user that elevated us, in most cases).
        string current = Environment.GetEnvironmentVariable("PATH") ?? "";
        parts.AddRange(current.Split(';', StringSplitOptions.RemoveEmptyEntries));

        // 2. Explicit target-user Environment Path. Reading it directly bypasses
        // the elevated process using the admin account's HKCU on some boxes.
        string? activeSid = UserProfileResolver.GetActiveUserSid(_log);
        string profile = UserProfileResolver.GetUserProfile(_log);
        string? userPath = UserProfileResolver.ReadUserRegistryValue(activeSid, "Environment", "Path");
        if (!string.IsNullOrEmpty(userPath))
        {
            userPath = userPath.Replace("%USERPROFILE%", profile, StringComparison.OrdinalIgnoreCase);
            foreach (string p in Environment.ExpandEnvironmentVariables(userPath)
                                .Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                parts.Add(p);
            }
        }

        // 3. Common per-user install locations the user might have used.
        if (!string.IsNullOrEmpty(profile))
        {
            parts.Add(Path.Combine(profile, "AppData", "Local", "Programs", "Git", "cmd"));
            parts.Add(Path.Combine(profile, "AppData", "Local", "Programs", "Git", "bin"));
            parts.Add(Path.Combine(profile, "AppData", "Local", "Programs", "Python", "Python312"));
            parts.Add(Path.Combine(profile, "AppData", "Local", "Programs", "Python", "Python313"));
        }
        // 4. Standard Git for Windows machine paths.
        parts.Add(@"C:\Program Files\Git\cmd");
        parts.Add(@"C:\Program Files\Git\bin");
        parts.Add(@"C:\Program Files (x86)\Git\cmd");

        // De-dupe while preserving order.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dedup = new List<string>(parts.Count);
        foreach (var p in parts)
        {
            string n = p.Trim().TrimEnd('\\');
            if (n.Length > 0 && seen.Add(n)) dedup.Add(n);
        }
        return string.Join(';', dedup);
    }

    /// <summary>Resolves an executable by walking the merged search path manually.</summary>
    private string? ResolveExecutable(string nameWithoutExt)
    {
        foreach (string dir in _searchPath.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string ext in new[] { ".exe", ".cmd", ".bat" })
            {
                string candidate = Path.Combine(dir, nameWithoutExt + ext);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }

    public async Task<EnvCheckResult> RunAsync()
    {
        var (osOk, isWindows10, osDisplay) = CheckOs();
        var (archOk, archDisplay) = CheckArch();
        var (gitOk, gitDisp) = await CheckGitAsync();
        var (pyOk, pyDisp)   = await CheckPythonAsync();
        var (wtOk, wtDisp)   = CheckWindowsTerminal(isWindows10);
        return new EnvCheckResult(osOk, isWindows10, osDisplay, archOk, archDisplay, gitOk, gitDisp, pyOk, pyDisp, wtOk, wtDisp);
    }

    private (bool ok, bool isWindows10, string display) CheckOs()
    {
        var v = Environment.OSVersion.Version;
        bool ok = v.Major >= 10 && v.Build >= MinWin10Build;
        bool isWindows11 = v.Major == 10 && v.Build >= MinWin11Build;
        bool isWindows10 = ok && !isWindows11;
        string family = isWindows11 ? "Windows 11" : ok ? "Windows 10" : $"Windows {v.Major}.{v.Minor}";
        string display = $"{family} (build {v.Build})";
        _log.Info($"OS check: {display} -> {(ok ? "OK" : $"FAIL (need Windows 10 build {MinWin10Build}+)")}");
        return (ok, isWindows10, display);
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
            string? exe = ResolveExecutable("git");
            if (exe is null)
            {
                _log.Info("Git check: not found on the merged search path (HKLM + HKCU + common dirs)");
                return (false, null);
            }
            _log.Info($"Git check: probing {exe}");
            string? raw = await RunCaptureAsync(exe, new[] { "--version" }, timeoutMs: 5000);
            if (string.IsNullOrWhiteSpace(raw)) { _log.Info("Git check: probe returned empty output"); return (false, null); }
            var m = Regex.Match(raw, @"git version (\d+)\.(\d+)(?:\.(\d+))?");
            if (!m.Success) { _log.Info($"Git check: unparseable -> {raw.Trim()}"); return (false, raw.Trim()); }
            int major = int.Parse(m.Groups[1].Value);
            int minor = int.Parse(m.Groups[2].Value);
            bool ok = major > MinGitMajor || (major == MinGitMajor && minor >= MinGitMinor);
            string disp = $"Git {major}.{minor}" + (m.Groups[3].Success ? $".{m.Groups[3].Value}" : "");
            _log.Info($"Git check: {disp} -> {(ok ? "OK" : $"too old (need >= {MinGitMajor}.{MinGitMinor})")}");
            return (ok, disp);
        }
        catch (Exception ex)
        {
            _log.Info($"Git check: probe error -> {ex.Message}");
            return (false, null);
        }
    }

    private (bool, string?) CheckWindowsTerminal(bool isWindows10)
    {
        if (!isWindows10)
        {
            _log.Info("Windows Terminal check: skipped (not Windows 10)");
            return (true, null);
        }

        string? exe = AxiomateLauncher.TryResolveWindowsTerminal();
        if (exe is null)
        {
            _log.Info("Windows Terminal check: wt.exe not found");
            return (false, null);
        }

        _log.Info($"Windows Terminal check: found {exe} -> OK");
        return (true, "Windows Terminal");
    }

    private async Task<(bool, string?)> CheckPythonAsync()
    {
        // Try py.exe -3.12 first (it's HKLM and uses its own version-resolving DB),
        // then python(.exe) on the merged PATH.
        string? py = ResolveExecutable("py");
        if (py is not null)
        {
            string? raw = await RunCaptureAsync(py, new[] { "-3.12", "--version" }, timeoutMs: 5000);
            var parsed = TryParsePython(raw);
            if (parsed is { } p1)
            {
                _log.Info($"Python check: {p1.disp} -> {(p1.ok ? "OK" : "too old")}");
                return (p1.ok, p1.disp);
            }
        }
        foreach (string name in new[] { "python", "python3" })
        {
            string? exe = ResolveExecutable(name);
            if (exe is null) continue;
            string? raw = await RunCaptureAsync(exe, new[] { "--version" }, timeoutMs: 5000);
            var parsed = TryParsePython(raw);
            if (parsed is { } p2)
            {
                _log.Info($"Python check: {p2.disp} -> {(p2.ok ? "OK" : "too old")}");
                return (p2.ok, p2.disp);
            }
        }
        _log.Info("Python check: not found");
        return (false, null);
    }

    private static (bool ok, string disp)? TryParsePython(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var m = Regex.Match(raw, @"Python (\d+)\.(\d+)\.(\d+)");
        if (!m.Success) return null;
        int major = int.Parse(m.Groups[1].Value);
        int minor = int.Parse(m.Groups[2].Value);
        int patch = int.Parse(m.Groups[3].Value);
        bool ok = major > MinPyMajor || (major == MinPyMajor && minor >= MinPyMinor);
        return (ok, $"Python {major}.{minor}.{patch}");
    }

    private static async Task<string?> RunCaptureAsync(string exe, string[] args, int timeoutMs)
    {
        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (string arg in args) psi.ArgumentList.Add(arg);
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
