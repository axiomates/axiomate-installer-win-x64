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

    /// <summary>Set execution policy to RemoteSigned. Windows Settings' developer-page
    /// PowerShell toggle is user-facing, so write CurrentUser as well as LocalMachine.</summary>
    private async Task SetPowerShellExecutionPolicyAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command " +
                "\"Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope LocalMachine -Force; " +
                "Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force; " +
                "Get-ExecutionPolicy -List\"")
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
                _log.Info("PowerShell ExecutionPolicy: set to RemoteSigned (LocalMachine + CurrentUser).");
            else
                _log.Warn($"PowerShell ExecutionPolicy: powershell.exe exited with {p.ExitCode}.");
        }
        catch (Exception ex)
        {
            _log.Warn($"PowerShell ExecutionPolicy: {ex.Message}");
        }

        WritePowerShellExecutionPolicyRegistryFallback();
    }

    private void WritePowerShellExecutionPolicyRegistryFallback()
    {
        SetExecutionPolicyValue(Registry.LocalMachine, @"SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell");
        SetExecutionPolicyValue(Registry.CurrentUser,  @"SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell");
        SetExecutionPolicyValue(Registry.LocalMachine, @"SOFTWARE\Microsoft\PowerShellCore\ShellIds\Microsoft.PowerShell");
        SetExecutionPolicyValue(Registry.CurrentUser,  @"SOFTWARE\Microsoft\PowerShellCore\ShellIds\Microsoft.PowerShell");

        string? activeSid = UserProfileResolver.GetActiveUserSid(_log);
        if (!string.IsNullOrWhiteSpace(activeSid))
        {
            SetExecutionPolicyValue(Registry.Users, activeSid + @"\SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell");
            SetExecutionPolicyValue(Registry.Users, activeSid + @"\SOFTWARE\Microsoft\PowerShellCore\ShellIds\Microsoft.PowerShell");
        }
    }

    private void SetExecutionPolicyValue(RegistryKey root, string subKey)
    {
        try
        {
            using var key = root.CreateSubKey(subKey, writable: true);
            if (key is null)
            {
                _log.Warn($"PowerShell ExecutionPolicy: cannot open {root.Name}\\{subKey}; skipping.");
                return;
            }
            key.SetValue("ExecutionPolicy", "RemoteSigned", RegistryValueKind.String);
            _log.Info($"PowerShell ExecutionPolicy: wrote {root.Name}\\{subKey} = RemoteSigned.");
        }
        catch (Exception ex)
        {
            _log.Warn($"PowerShell ExecutionPolicy registry fallback: {root.Name}\\{subKey}: {ex.Message}");
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
