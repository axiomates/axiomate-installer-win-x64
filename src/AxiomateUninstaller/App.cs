using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace AxiomateUninstaller;

public partial class App : System.Windows.Application
{
    [STAThread]
    public static int Main(string[] args)
    {
        bool quiet = args.Any(a => string.Equals(a, "/quiet", StringComparison.OrdinalIgnoreCase));
        try
        {
            // Detect install location.
            string installDir = ResolveInstallDir();
            if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
            {
                if (!quiet) MessageBox.Show("找不到 Axiomate 安装目录，可能已被手动删除。", "卸载 Axiomate",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                // Still try to clean registry.
            }

            if (!quiet)
            {
                var res = MessageBox.Show(
                    "确认卸载 Axiomate？\n\n" +
                    "您的 ~/.axiomate.json 配置文件以及 ~/.axiomate/ 目录将被保留。",
                    "卸载 Axiomate",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);
                if (res != MessageBoxResult.OK) return 0;
            }

            // 1. Remove PATH entry.
            try { RemoveFromPath(installDir); } catch (Exception ex) { LogIgnore("PATH cleanup failed", ex); }

            // 2. Remove shortcuts.
            try { RemoveShortcuts(); } catch (Exception ex) { LogIgnore("shortcut cleanup failed", ex); }

            // 3. Remove Apps & features registry.
            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Axiomate",
                    throwOnMissingSubKey: false);
            }
            catch (Exception ex) { LogIgnore("registry cleanup failed", ex); }

            // 4. Schedule self + dir removal.
            string ownExe = Process.GetCurrentProcess().MainModule!.FileName!;
            ScheduleSelfDelete(installDir, ownExe);

            if (!quiet)
            {
                MessageBox.Show("Axiomate 已卸载。",
                    "卸载完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            return 0;
        }
        catch (Exception ex)
        {
            if (!quiet)
            {
                MessageBox.Show($"卸载过程中遇到错误：\n{ex.Message}",
                    "卸载失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return 1;
        }
    }

    private static string ResolveInstallDir()
    {
        // First try the registry value the installer wrote.
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Axiomate");
            if (key?.GetValue("InstallLocation") is string s && !string.IsNullOrWhiteSpace(s))
                return s;
        }
        catch { }
        // Fall back to the directory the uninstaller is running from.
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

        // Broadcast.
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

    private static void ScheduleSelfDelete(string installDir, string ownExe)
    {
        // Use cmd.exe to wait a couple of seconds then nuke the install dir
        // (which contains Uninstaller.exe itself).
        if (string.IsNullOrEmpty(installDir)) return;
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c ping -n 3 127.0.0.1 >nul & rd /s /q \"{installDir}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(psi);
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

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, IntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
}
