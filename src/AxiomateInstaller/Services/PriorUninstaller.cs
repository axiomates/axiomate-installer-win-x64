using System;
using System.IO;

namespace AxiomateInstaller.Services;

/// <summary>
/// Removes a previously installed Axiomate from the new installer process
/// (already elevated), mirroring AxiomateUninstaller's steps exactly: clear the
/// HKLM PATH entry, delete Desktop/Start-menu shortcuts, remove the Apps &amp;
/// features registry key, then force-delete the old install dir after the same
/// safety checks. Runs synchronously and never touches ~/.axiomate.json or
/// ~/.axiomate/ (preserved, like the standalone uninstaller).
/// </summary>
public sealed class PriorUninstaller
{
    private readonly Logger _log;
    public PriorUninstaller(Logger log) { _log = log; }

    public void Run(string installDir)
    {
        if (string.IsNullOrWhiteSpace(installDir))
        {
            _log.Warn("Prior install dir empty; skipping uninstall.");
            return;
        }

        _log.Info($"Removing prior Axiomate install at {installDir}");

        try { new PathRegistrar(_log).RemoveFromPath(installDir); }
        catch (Exception ex) { _log.Warn($"PATH cleanup failed: {ex.Message}"); }

        try { RemoveShortcuts(); }
        catch (Exception ex) { _log.Warn($"Shortcut cleanup failed: {ex.Message}"); }

        try { UninstallRegistrar.Unregister(_log); }
        catch (Exception ex) { _log.Warn($"Registry cleanup failed: {ex.Message}"); }

        try
        {
            if (Directory.Exists(installDir))
            {
                EnsureSafeInstallDirForDeletion(installDir);
                new RunningProcessGuard(_log).TerminateProcessesUnder(installDir);
                ForceDeleteDirectory(installDir);
                _log.Info($"Prior install dir removed: {installDir}");
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Could not remove prior install dir {installDir}: {ex.Message}");
        }
    }

    private void RemoveShortcuts()
    {
        var sm = new ShortcutManager(_log);
        string startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");
        string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        sm.Delete(Path.Combine(startMenuDir, "Axiomate.lnk"));
        sm.Delete(Path.Combine(desktopDir, "Axiomate.lnk"));
    }

    /// <summary>Mirror of AxiomateUninstaller.EnsureSafeInstallDirForDeletion:
    /// refuse to delete unsafe or unverified directories.</summary>
    private static void EnsureSafeInstallDirForDeletion(string installDir)
    {
        string full = PathCanonicalizer.CanonicalizeForComparison(installDir);
        _ = File.GetAttributes(full);
        string root = Path.GetPathRoot(full)?.TrimEnd('\\', '/') ?? "";
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
        string[] forbidden =
        {
            root,
            Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\', '/'),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\', '/'),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd('\\', '/'),
            userProfile,
            Path.Combine(userProfile, "Desktop"),
            Path.Combine(userProfile, "Documents"),
            Path.Combine(userProfile, "Downloads"),
            Path.GetTempPath().TrimEnd('\\', '/'),
        };

        foreach (string forbiddenPath in forbidden)
        {
            if (!string.IsNullOrWhiteSpace(forbiddenPath) &&
                string.Equals(full, PathCanonicalizer.CanonicalizeForComparison(forbiddenPath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Refusing to delete unsafe install directory: {full}");
            }
        }

        bool hasManifest = File.Exists(Path.Combine(full, "installation-manifest.json"));
        bool hasAxiomateExe = File.Exists(Path.Combine(full, "axiomate.exe"));
        bool hasUninstaller = File.Exists(Path.Combine(full, "Uninstaller.exe"));
        if (!hasManifest || !hasAxiomateExe || !hasUninstaller)
        {
            throw new InvalidOperationException($"Refusing to delete unverified install directory: {full}");
        }
    }

    private static void ForceDeleteDirectory(string dir)
    {
        foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
        }
        Directory.Delete(dir, recursive: true);
    }
}
