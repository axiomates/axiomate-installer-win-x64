using System.IO;
using Microsoft.Win32;

namespace AxiomateInstaller.Services;

public sealed record PriorInstall(string InstallDir, string UninstallerExe);

/// <summary>
/// Detects a previous Axiomate install via the Apps &amp; features uninstall key
/// so the wizard can offer to remove it before deploying a fresh copy.
/// </summary>
public static class PriorInstallDetector
{
    public static PriorInstall? Detect()
    {
        using var key = Registry.LocalMachine.OpenSubKey(UninstallRegistrar.UninstallKey);
        if (key is null) return null;

        string installDir = key.GetValue("InstallLocation") as string ?? "";
        if (string.IsNullOrWhiteSpace(installDir)) return null;

        string uninstallerExe = Path.Combine(installDir, "Uninstaller.exe");
        if (!File.Exists(uninstallerExe)) return null;

        return new PriorInstall(installDir.TrimEnd('\\'), uninstallerExe);
    }
}
