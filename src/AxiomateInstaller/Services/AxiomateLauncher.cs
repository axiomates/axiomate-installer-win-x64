using System;
using System.Diagnostics;
using System.IO;

namespace AxiomateInstaller.Services;

public static class AxiomateLauncher
{
    public static string ResolveWindowsTerminal()
    {
        string userProfile = UserProfileResolver.GetUserProfile();
        string userWt = Path.Combine(userProfile, "AppData", "Local", "Microsoft", "WindowsApps", "wt.exe");
        if (File.Exists(userWt)) return userWt;

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string localWt = Path.Combine(localAppData, "Microsoft", "WindowsApps", "wt.exe");
        if (File.Exists(localWt)) return localWt;

        return "wt.exe";
    }

    public static string BuildWindowsTerminalArguments(string axiomateExe, string workspaceDir)
    {
        return $"-d {QuoteArg(workspaceDir)} {QuoteArg(axiomateExe)} {QuoteArg(workspaceDir)}";
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
