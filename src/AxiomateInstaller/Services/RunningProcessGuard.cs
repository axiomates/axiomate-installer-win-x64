using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AxiomateInstaller.Services;

/// <summary>
/// Before the installer wipes the target directory, terminate any Axiomate
/// processes still running *out of that directory* (axiomate.exe and the native
/// helpers it spawns: agent-browser.exe, rg.exe, rtk.exe). A leftover process
/// keeps an open handle to its own image, which makes Directory.Delete fail with
/// "Access to the path 'axiomate.exe' is denied" — the exact failure users hit
/// when re-installing over a copy that is still running.
///
/// Matching is by *executable path under installDir*, never by name alone:
/// rg.exe / rtk.exe / agent-browser.exe are common tool names, so killing them
/// blindly could take out an unrelated ripgrep (or similar) the user is running
/// elsewhere. We only touch processes whose image actually lives in the dir we
/// are about to delete.
/// </summary>
public sealed class RunningProcessGuard
{
    private static readonly string[] ProcessNames =
    {
        "axiomate", "agent-browser", "rg", "rtk"
    };

    private readonly Logger _log;
    public RunningProcessGuard(Logger log) { _log = log; }

    /// <summary>
    /// Kills every candidate process whose image lives under <paramref name="installDir"/>,
    /// then waits for each to exit so the OS releases the file handles. Best-effort:
    /// failures are logged, never thrown, so a process we cannot inspect (e.g. an
    /// access-denied MainModule) does not abort the install.
    /// </summary>
    public void TerminateProcessesUnder(string installDir)
    {
        if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
            return;

        string canonicalDir = PathCanonicalizer.CanonicalizeForComparison(installDir);
        var killed = new List<Process>();

        foreach (string name in ProcessNames)
        {
            Process[] procs;
            try { procs = Process.GetProcessesByName(name); }
            catch (Exception ex) { _log.Warn($"Enumerating '{name}' processes failed: {ex.Message}"); continue; }

            foreach (var p in procs)
            {
                string? exePath = TryGetExecutablePath(p);
                if (exePath is null) { p.Dispose(); continue; }

                string canonicalExe = PathCanonicalizer.CanonicalizeForComparison(exePath);
                if (!IsUnder(canonicalExe, canonicalDir)) { p.Dispose(); continue; }

                try
                {
                    _log.Info($"Terminating running process {p.ProcessName} (pid {p.Id}) at {exePath}");
                    p.Kill(entireProcessTree: true);
                    killed.Add(p);
                }
                catch (Exception ex)
                {
                    _log.Warn($"Could not kill {p.ProcessName} (pid {p.Id}): {ex.Message}");
                    p.Dispose();
                }
            }
        }

        foreach (var p in killed)
        {
            try
            {
                if (!p.WaitForExit(10000))
                    _log.Warn($"Process {p.ProcessName} (pid {p.Id}) did not exit within timeout.");
            }
            catch (Exception ex) { _log.Warn($"WaitForExit for {p.ProcessName} failed: {ex.Message}"); }
            finally { p.Dispose(); }
        }

        if (killed.Count > 0)
        {
            // Give Windows a brief moment to release the image handles after the
            // processes exit, before the caller deletes the directory.
            Thread.Sleep(300);
        }
    }

    private string? TryGetExecutablePath(Process p)
    {
        try { return p.MainModule?.FileName; }
        catch (Exception ex)
        {
            _log.Warn($"Cannot read module path of {p.ProcessName} (pid {p.Id}): {ex.Message}");
            return null;
        }
    }

    private static bool IsUnder(string path, string root)
    {
        string normalizedPath = path.TrimEnd('\\');
        string normalizedRoot = root.TrimEnd('\\');
        return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(normalizedRoot + "\\", StringComparison.OrdinalIgnoreCase);
    }
}
