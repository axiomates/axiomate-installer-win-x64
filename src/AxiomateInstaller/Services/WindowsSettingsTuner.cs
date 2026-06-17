using System;
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

    public Task ApplyAsync()
    {
        SetPowerShellExecutionPolicy();
        EnableDeveloperMode();
        return Task.CompletedTask;
    }

    /// <summary>Set execution policy to RemoteSigned via registry APIs instead of invoking powershell.exe.</summary>
    private void SetPowerShellExecutionPolicy()
    {
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
