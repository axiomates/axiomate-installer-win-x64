using System;
using System.IO;

namespace AxiomateInstaller.Services;

public static class WorkspacePathGuard
{
    public static string ResolveForUser(string path, string userProfile)
    {
        string resolved = path.Trim();
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            string root = Path.GetPathRoot(userProfile)?.TrimEnd('\\') ?? "";
            string leaf = userProfile.Length > root.Length ? userProfile[root.Length..].TrimStart('\\') : "";
            resolved = resolved.Replace("%USERPROFILE%", userProfile, StringComparison.OrdinalIgnoreCase)
                               .Replace("%HOMEDRIVE%", root, StringComparison.OrdinalIgnoreCase)
                               .Replace("%HOMEPATH%", "\\" + leaf, StringComparison.OrdinalIgnoreCase)
                               .Replace("%LOCALAPPDATA%", Path.Combine(userProfile, "AppData", "Local"), StringComparison.OrdinalIgnoreCase)
                               .Replace("%APPDATA%", Path.Combine(userProfile, "AppData", "Roaming"), StringComparison.OrdinalIgnoreCase);
        }
        resolved = Environment.ExpandEnvironmentVariables(resolved);
        if (resolved.Contains('%')) throw new InstallStepException(Strings.Get("Opt_BadWorkspaceBody_Invalid"));
        if (!Path.IsPathRooted(resolved)) throw new InstallStepException(Strings.Get("Opt_BadWorkspaceBody_Invalid"));
        string normalized = Normalize(resolved);
        var eval = DirGuard.Evaluate(normalized);
        if (!eval.Ok) throw new InstallStepException(eval.Reason ?? Strings.Get("Opt_BadWorkspaceBody_Invalid"));
        return normalized;
    }

    public static void EnsureSeparateFromInstallDir(string workspaceDir, string installDir, string userProfile)
    {
        string workspace = PathCanonicalizer.CanonicalizeForComparison(ResolveForUser(workspaceDir, userProfile));
        string install = PathCanonicalizer.CanonicalizeForComparison(installDir);

        if (string.Equals(workspace, install, StringComparison.OrdinalIgnoreCase) ||
            IsSubdirectoryOf(workspace, install) ||
            IsSubdirectoryOf(install, workspace))
        {
            throw new InstallStepException(Strings.Get("Opt_BadWorkspaceBody_OverlapsInstall"));
        }
    }

    private static string Normalize(string path)
    {
        return PathCanonicalizer.Normalize(path);
    }

    private static bool IsSubdirectoryOf(string child, string parent)
    {
        string childWithSlash = child + Path.DirectorySeparatorChar;
        string parentWithSlash = parent + Path.DirectorySeparatorChar;
        return childWithSlash.StartsWith(parentWithSlash, StringComparison.OrdinalIgnoreCase);
    }
}
