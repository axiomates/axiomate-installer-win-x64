using Microsoft.Win32;

namespace AxiomateInstaller.Services;

public sealed record PriorInstall(string InstallDir);

/// <summary>
/// Detects a previous Axiomate install via the Apps &amp; features uninstall key
/// so the wizard can offer to remove it before deploying a fresh copy. The
/// registry InstallLocation is the authoritative signal; we no longer require
/// the old Uninstaller.exe to be present since the new installer cleans up the
/// prior install itself (and a stale PATH/registry entry should still be
/// cleaned even if the old directory was deleted manually).
/// </summary>
public static class PriorInstallDetector
{
    public static PriorInstall? Detect()
    {
        using var key = Registry.LocalMachine.OpenSubKey(UninstallRegistrar.UninstallKey);
        if (key is null) return null;

        string installDir = key.GetValue("InstallLocation") as string ?? "";
        if (string.IsNullOrWhiteSpace(installDir)) return null;

        return new PriorInstall(installDir.TrimEnd('\\'));
    }
}
