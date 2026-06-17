using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace AxiomateUninstaller;

public partial class App : System.Windows.Application
{
    private static bool _zh;

    [STAThread]
    public static int Main(string[] args)
    {
        bool quiet = args.Any(a => string.Equals(a, "/quiet", StringComparison.OrdinalIgnoreCase));
        if (args.Length >= 4 && string.Equals(args[0], "--delete-install-dir", StringComparison.OrdinalIgnoreCase))
        {
            return RunDeleteHelper(args);
        }

        try { _zh = string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "zh",
                                   StringComparison.OrdinalIgnoreCase); } catch { _zh = false; }

        try
        {
            string installDir = ResolveInstallDir();
            if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
            {
                if (!quiet) MessageBox.Show(T("MissingDirBody"), T("Title"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            if (!quiet)
            {
                var res = MessageBox.Show(T("ConfirmBody"), T("Title"),
                    MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (res != MessageBoxResult.OK) return 0;
            }

            try { RemoveFromPath(installDir); } catch (Exception ex) { LogIgnore("PATH cleanup failed", ex); }
            try { RemoveShortcuts(); } catch (Exception ex) { LogIgnore("shortcut cleanup failed", ex); }
            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Axiomate",
                    throwOnMissingSubKey: false);
            }
            catch (Exception ex) { LogIgnore("registry cleanup failed", ex); }

            EnsureSafeInstallDirForDeletion(installDir);
            string ownExe = Process.GetCurrentProcess().MainModule!.FileName!;
            ScheduleSelfDelete(installDir, ownExe);

            if (!quiet)
            {
                MessageBox.Show(T("DoneBody"), T("DoneTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            return 0;
        }
        catch (Exception ex)
        {
            if (!quiet)
            {
                MessageBox.Show($"{T("FailBody")}\n{ex.Message}",
                    T("FailTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return 1;
        }
    }

    private static int RunDeleteHelper(string[] args)
    {
        try
        {
            string installDir = args[1];
            if (!int.TryParse(args[2], out int parentPid)) return 1;
            string helperExe = args[3];

            try
            {
                using var parent = Process.GetProcessById(parentPid);
                if (!parent.WaitForExit(30000)) return 1;
            }
            catch { }

            EnsureSafeInstallDirForDeletion(installDir);
            Directory.Delete(installDir, recursive: true);
            TryDeleteSelf(helperExe);
            return 0;
        }
        catch
        {
            return 1;
        }
    }

    private static string T(string key) => _zh ? Zh(key) : En(key);

    private static string Zh(string key) => key switch
    {
        "Title"          => "卸载 Axiomate",
        "MissingDirBody" => "找不到 Axiomate 安装目录, 可能已被手动删除。",
        "ConfirmBody"    => "确认卸载 Axiomate？\n\n您的 ~/.axiomate.json 配置文件以及 ~/.axiomate/ 目录将被保留。",
        "DoneTitle"      => "卸载完成",
        "DoneBody"       => "Axiomate 已卸载。",
        "FailTitle"      => "卸载失败",
        "FailBody"       => "卸载过程中遇到错误:",
        _                => key
    };

    private static string En(string key) => key switch
    {
        "Title"          => "Uninstall Axiomate",
        "MissingDirBody" => "Cannot find the Axiomate install directory; it may have been deleted manually.",
        "ConfirmBody"    => "Confirm uninstalling Axiomate?\n\nYour ~/.axiomate.json file and ~/.axiomate/ directory will be preserved.",
        "DoneTitle"      => "Uninstall complete",
        "DoneBody"       => "Axiomate has been uninstalled.",
        "FailTitle"      => "Uninstall failed",
        "FailBody"       => "An error occurred during uninstall:",
        _                => key
    };

    private static string ResolveInstallDir()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Axiomate");
            if (key?.GetValue("InstallLocation") is string s && !string.IsNullOrWhiteSpace(s))
                return s;
        }
        catch { }
        return Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName ?? "") ?? "";
    }

    private static void RemoveFromPath(string installDir)
    {
        if (string.IsNullOrEmpty(installDir)) return;
        const string envKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
        using var key = Registry.LocalMachine.OpenSubKey(envKey, writable: true);
        if (key is null) return;
        if (key.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) is not string current ||
            string.IsNullOrEmpty(current)) return;

        string normalized = installDir.TrimEnd('\\');
        var entries = current.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(e => !string.Equals(e.Trim().TrimEnd('\\'), normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();
        key.SetValue("Path", string.Join(';', entries), RegistryValueKind.ExpandString);

        try
        {
            IntPtr HWND_BROADCAST = (IntPtr)0xFFFF;
            uint WM_SETTINGCHANGE = 0x001A;
            uint SMTO_ABORTIFHUNG = 0x0002;
            SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "Environment",
                SMTO_ABORTIFHUNG, 5000, out _);
        }
        catch { }
    }

    private static void RemoveShortcuts()
    {
        string startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");
        string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        foreach (string p in new[]
        {
            Path.Combine(startMenuDir, "Axiomate.lnk"),
            Path.Combine(desktopDir,   "Axiomate.lnk"),
        })
        {
            try { if (File.Exists(p)) File.Delete(p); } catch { }
        }
    }

    private static void EnsureSafeInstallDirForDeletion(string installDir)
    {
        string full = CanonicalizeForComparison(installDir);
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
                string.Equals(full, CanonicalizeForComparison(forbiddenPath), StringComparison.OrdinalIgnoreCase))
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

    private static string CanonicalizeForComparison(string path)
    {
        string normalized = Path.GetFullPath(path).TrimEnd('\\', '/');
        string? final = TryGetFinalPath(normalized);
        return string.IsNullOrWhiteSpace(final) ? normalized : final.TrimEnd('\\', '/');
    }

    private static string? TryGetFinalPath(string path)
    {
        try
        {
            using SafeFileHandle handle = CreateFileW(
                path,
                0,
                0x00000001 | 0x00000002 | 0x00000004,
                IntPtr.Zero,
                3,
                0x02000000,
                IntPtr.Zero);
            if (handle.IsInvalid) return null;
            var sb = new StringBuilder(512);
            uint len = GetFinalPathNameByHandleW(handle, sb, (uint)sb.Capacity, 0);
            if (len == 0) return null;
            if (len > sb.Capacity)
            {
                sb.EnsureCapacity((int)len);
                len = GetFinalPathNameByHandleW(handle, sb, (uint)sb.Capacity, 0);
                if (len == 0) return null;
            }
            string final = sb.ToString();
            const string prefix = @"\\?\";
            return final.StartsWith(prefix, StringComparison.Ordinal) ? final[prefix.Length..] : final;
        }
        catch { return null; }
    }

    private static void ScheduleSelfDelete(string installDir, string ownExe)
    {
        if (string.IsNullOrEmpty(installDir)) return;
        string helperDir = Path.Combine(Path.GetTempPath(), "axiomate-installer");
        Directory.CreateDirectory(helperDir);
        string helperExe = Path.Combine(helperDir, $"axiomate-uninstall-delete-{Guid.NewGuid():N}.exe");
        File.Copy(ownExe, helperExe, overwrite: true);

        var psi = new ProcessStartInfo(helperExe)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        psi.ArgumentList.Add("--delete-install-dir");
        psi.ArgumentList.Add(installDir);
        psi.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(helperExe);
        Process.Start(psi);
    }

    private static void TryDeleteSelf(string helperExe)
    {
        try { MoveFileEx(helperExe, null, MOVEFILE_DELAY_UNTIL_REBOOT); }
        catch { }
    }

    private static void LogIgnore(string msg, Exception ex)
    {
        try
        {
            string dir = Path.Combine(Path.GetTempPath(), "axiomate-installer");
            Directory.CreateDirectory(dir);
            string log = Path.Combine(dir, $"uninstall-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.AppendAllText(log, $"[WARN] {msg}: {ex}\r\n");
        }
        catch { }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle hFile,
        [Out] StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags);

    private const int MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string lpExistingFileName, string? lpNewFileName, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, IntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
}
