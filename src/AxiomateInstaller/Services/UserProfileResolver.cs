using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace AxiomateInstaller.Services;

public static class UserProfileResolver
{
    public static string GetUserProfile(Logger? log = null)
    {
        string? activeSid = TargetUserContext.Sid ?? GetActiveUserSid(log);
        foreach (string? candidate in new[]
        {
            TargetUserContext.Profile,
            ReadUserRegistryValue(activeSid, @"Volatile Environment", "USERPROFILE"),
            ReadUserRegistryValue(activeSid, @"Environment", "USERPROFILE"),
            ReadProfileImagePath(activeSid),
            ReadRegistryValue(Registry.CurrentUser, @"Volatile Environment", "USERPROFILE"),
            Environment.GetEnvironmentVariable("USERPROFILE", EnvironmentVariableTarget.User),
            Environment.GetEnvironmentVariable("USERPROFILE"),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        })
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                string expanded = Environment.ExpandEnvironmentVariables(candidate).TrimEnd('\\');
                log?.Info($"Resolved user profile: {expanded}");
                return expanded;
            }
        }

        log?.Warn("Could not resolve user profile from active user SID, HKCU Volatile Environment, USERPROFILE, or SpecialFolder.UserProfile.");
        return "";
    }

    public static string? GetActiveUserSid(Logger? log = null)
    {
        if (!string.IsNullOrWhiteSpace(TargetUserContext.Sid)) return TargetUserContext.Sid;
        try
        {
            int activeSession = unchecked((int)WTSGetActiveConsoleSessionId());
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                using (process)
                {
                    if (activeSession >= 0 && process.SessionId != activeSession) continue;
                    if (!OpenProcessToken(process.Handle, TOKEN_QUERY, out IntPtr token)) continue;
                    try
                    {
                        using var identity = new WindowsIdentity(token);
                        string? sid = identity.User?.Value;
                        if (!string.IsNullOrWhiteSpace(sid))
                        {
                            log?.Info($"Resolved active user SID: {sid}");
                            return sid;
                        }
                    }
                    finally
                    {
                        CloseHandle(token);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log?.Warn($"Could not resolve active user SID: {ex.Message}");
        }
        return null;
    }

    public static string? ReadUserRegistryValue(string? sid, string subKey, string name)
    {
        if (string.IsNullOrWhiteSpace(sid)) return null;
        return ReadRegistryValue(Registry.Users, sid + @"\" + subKey, name);
    }

    private static string? ReadProfileImagePath(string? sid)
    {
        if (string.IsNullOrWhiteSpace(sid)) return null;
        return ReadRegistryValue(
            Registry.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\" + sid,
            "ProfileImagePath");
    }

    private static string? ReadRegistryValue(RegistryKey root, string subKey, string name)
    {
        try
        {
            using var key = root.OpenSubKey(subKey);
            return key?.GetValue(name) as string;
        }
        catch
        {
            return null;
        }
    }

    private const uint TOKEN_QUERY = 0x0008;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();
}
