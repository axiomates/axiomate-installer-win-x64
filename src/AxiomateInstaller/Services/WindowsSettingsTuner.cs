using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace AxiomateInstaller.Services;

/// <summary>
/// Forces two Windows-side settings that axiomate workflows depend on. Both are
/// HKLM writes that need admin (already required by the installer manifest).
///
///  1. Set machine-level PowerShell ExecutionPolicy to RemoteSigned, matching
///     the "Settings → System → For developers → PowerShell" toggle. Lets users
///     run unsigned local .ps1 helpers and signed remote scripts.
///
///  2. Enable Developer Mode (AllowAllTrustedApps + AllowDevelopmentWithoutDevLicense
///     under HKLM\…\AppModelUnlock). Matches "Settings → For developers → Developer Mode".
///
/// Both are required by axiomate at runtime, so the installer applies them
/// unconditionally — no UI option to opt out.
/// </summary>
public sealed class WindowsSettingsTuner
{
    private readonly Logger _log;
    public WindowsSettingsTuner(Logger log) { _log = log; }

    public async Task ApplyAsync()
    {
        await SetPowerShellExecutionPolicyAsync();
        EnableDeveloperMode();
    }

    /// <summary>Set machine-level execution policy to RemoteSigned via powershell.exe
    /// (the canonical way; equivalent to the Settings UI toggle).</summary>
    private async Task SetPowerShellExecutionPolicyAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command " +
                "\"Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope LocalMachine -Force\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            if (p is null)
            {
                _log.Warn("PowerShell ExecutionPolicy: could not launch powershell.exe; skipping.");
                return;
            }
            string stdout = await p.StandardOutput.ReadToEndAsync();
            string stderr = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            if (!string.IsNullOrWhiteSpace(stdout)) _log.Info("[ps stdout] " + stdout.Trim());
            if (!string.IsNullOrWhiteSpace(stderr)) _log.Info("[ps stderr] " + stderr.Trim());
            if (p.ExitCode == 0)
                _log.Info("PowerShell ExecutionPolicy: set to RemoteSigned (LocalMachine).");
            else
                _log.Warn($"PowerShell ExecutionPolicy: powershell.exe exited with {p.ExitCode}.");
        }
        catch (Exception ex)
        {
            _log.Warn($"PowerShell ExecutionPolicy: {ex.Message}");
        }
    }

    private void EnableDeveloperMode()
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock", writable: true);
            if (key is null)
            {
                _log.Warn("Developer Mode: cannot open HKLM AppModelUnlock; skipping.");
                return;
            }
            key.SetValue("AllowAllTrustedApps",                1, RegistryValueKind.DWord);
            key.SetValue("AllowDevelopmentWithoutDevLicense",  1, RegistryValueKind.DWord);
            _log.Info("Developer Mode: enabled (AllowAllTrustedApps + AllowDevelopmentWithoutDevLicense).");
        }
        catch (Exception ex)
        {
            _log.Warn($"Developer Mode: {ex.Message}");
        }
    }
}
