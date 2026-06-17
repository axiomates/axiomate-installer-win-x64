using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AxiomateInstaller.Services;

/// <summary>
/// Adds the install dir to the system-level PATH (HKLM) and broadcasts
/// WM_SETTINGCHANGE so newly opened shells pick it up.
/// </summary>
public sealed class PathRegistrar
{
    private const string EnvKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";

    private readonly Logger _log;
    public PathRegistrar(Logger log) { _log = log; }

    public void EnsureInPath(string dir)
    {
        using var key = Registry.LocalMachine.OpenSubKey(EnvKey, writable: true)
            ?? throw new InstallStepException(Strings.Get("Err_Reg_PathOpenFail"));

        // Read raw path value as ExpandString to avoid expanding %SystemRoot% etc.
        string current = "";
        var raw = key.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (raw is string s) current = s;

        var entries = current.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim().TrimEnd('\\'))
            .ToList();

        string normalized = dir.TrimEnd('\\');
        bool already = entries.Any(e => string.Equals(e, normalized, StringComparison.OrdinalIgnoreCase));
        if (already)
        {
            _log.Info($"PATH already contains {normalized}");
            BroadcastEnvChange();
            return;
        }

        entries.Add(normalized);
        string joined = string.Join(';', entries);
        key.SetValue("Path", joined, RegistryValueKind.ExpandString);
        _log.Info($"PATH updated (HKLM). Added: {normalized}");
        BroadcastEnvChange();
    }

    public void RemoveFromPath(string dir)
    {
        using var key = Registry.LocalMachine.OpenSubKey(EnvKey, writable: true);
        if (key is null)
        {
            _log.Warn("Cannot open HKLM Environment for write; skipping PATH removal.");
            return;
        }

        var raw = key.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (raw is not string current || string.IsNullOrEmpty(current))
        {
            _log.Info("PATH was empty; nothing to remove.");
            BroadcastEnvChange();
            return;
        }

        string normalized = dir.TrimEnd('\\');
        var entries = current.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(e => !string.Equals(e.Trim().TrimEnd('\\'), normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();
        string joined = string.Join(';', entries);
        key.SetValue("Path", joined, RegistryValueKind.ExpandString);
        _log.Info($"PATH updated (HKLM). Removed: {normalized}");
        BroadcastEnvChange();
    }

    private void BroadcastEnvChange()
    {
        try
        {
            IntPtr HWND_BROADCAST = (IntPtr)0xFFFF;
            uint WM_SETTINGCHANGE = 0x001A;
            uint SMTO_ABORTIFHUNG = 0x0002;
            SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "Environment",
                SMTO_ABORTIFHUNG, 5000, out _);
        }
        catch (Exception ex)
        {
            _log.Warn($"WM_SETTINGCHANGE broadcast failed: {ex.Message}");
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, IntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
}
