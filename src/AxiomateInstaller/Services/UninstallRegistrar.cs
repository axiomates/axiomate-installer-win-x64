using System;
using System.IO;
using Microsoft.Win32;

namespace AxiomateInstaller.Services;

/// <summary>
/// Registers an entry under HKLM\...\Uninstall\Axiomate so the app shows
/// up in Apps & features. Also stages the uninstaller exe at the install
/// dir.
/// </summary>
public sealed class UninstallRegistrar
{
    public const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Axiomate";

    private readonly Logger _log;
    public UninstallRegistrar(Logger log) { _log = log; }

    public void Register(
        string installDir,
        string uninstallerSourcePath,
        string axiomateVersion,
        string installerVersion,
        long estimatedBytes)
    {
        string uninstallerDest = Path.Combine(installDir, "Uninstaller.exe");
        File.Copy(uninstallerSourcePath, uninstallerDest, overwrite: true);
        _log.Info($"Uninstaller staged: {uninstallerDest}");

        using var key = Registry.LocalMachine.CreateSubKey(UninstallKey, writable: true)
            ?? throw new InstallStepException("无法创建注册表项 Uninstall\\Axiomate。");

        key.SetValue("DisplayName", "Axiomate");
        key.SetValue("DisplayVersion", axiomateVersion);
        key.SetValue("Publisher", "Axiomate");
        key.SetValue("URLInfoAbout", "https://axiomate.net");
        key.SetValue("Comments", $"Installer {installerVersion}");
        key.SetValue("InstallLocation", installDir);
        key.SetValue("UninstallString", $"\"{uninstallerDest}\"");
        key.SetValue("QuietUninstallString", $"\"{uninstallerDest}\" /quiet");
        key.SetValue("DisplayIcon", $"{Path.Combine(installDir, "axiomate.exe")},0");
        key.SetValue("EstimatedSize", (int)Math.Min(estimatedBytes / 1024, int.MaxValue), RegistryValueKind.DWord);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        _log.Info("Registered Apps & features entry: Axiomate");
    }

    public static void Unregister(Logger log)
    {
        try
        {
            Registry.LocalMachine.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);
            log.Info("Apps & features entry removed.");
        }
        catch (Exception ex)
        {
            log.Warn($"Could not remove Uninstall registry key: {ex.Message}");
        }
    }
}
