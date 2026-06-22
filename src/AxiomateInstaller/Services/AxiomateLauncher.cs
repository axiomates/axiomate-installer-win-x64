using System;
using System.Diagnostics;
using System.IO;

namespace AxiomateInstaller.Services;

public static class AxiomateLauncher
{
    public static string ResolveWindowsTerminal() => TryResolveWindowsTerminal() ?? "wt.exe";

    public static string? TryResolveWindowsTerminal()
    {
        string userProfile = UserProfileResolver.GetUserProfile();
        string userWt = Path.Combine(userProfile, "AppData", "Local", "Microsoft", "WindowsApps", "wt.exe");
        if (File.Exists(userWt)) return userWt;

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string localWt = Path.Combine(localAppData, "Microsoft", "WindowsApps", "wt.exe");
        if (File.Exists(localWt)) return localWt;

        string? pathWt = ResolveOnPath("wt.exe");
        if (pathWt is not null) return pathWt;

        return null;
    }

    private static string? ResolveOnPath(string fileName)
    {
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public static string BuildWindowsTerminalArguments(string axiomateExe, string workspaceDir)
    {
        return $"-d {QuoteArg(workspaceDir)} {QuoteArg(axiomateExe)}";
    }

    public static void LaunchUnelevatedFromShortcut()
    {
        string desktopShortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            "Axiomate.lnk");
        string startMenuShortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            "Programs",
            "Axiomate.lnk");
        string shortcut = File.Exists(desktopShortcut) ? desktopShortcut : startMenuShortcut;

        var psi = new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(shortcut);
        Process.Start(psi);
    }

    private static string QuoteArg(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
