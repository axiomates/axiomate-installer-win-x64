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
            resolved = resolved.Replace("%USERPROFILE%", userProfile, StringComparison.OrdinalIgnoreCase);
        }
        resolved = Environment.ExpandEnvironmentVariables(resolved);
        return Normalize(resolved);
    }

    public static void EnsureSeparateFromInstallDir(string workspaceDir, string installDir, string userProfile)
    {
        string workspace = ResolveForUser(workspaceDir, userProfile);
        string install = Normalize(installDir);

        if (string.Equals(workspace, install, StringComparison.OrdinalIgnoreCase) ||
            IsSubdirectoryOf(workspace, install) ||
            IsSubdirectoryOf(install, workspace))
        {
            throw new InstallStepException(Strings.Get("Opt_BadWorkspaceBody_OverlapsInstall"));
        }
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsSubdirectoryOf(string child, string parent)
    {
        string childWithSlash = child + Path.DirectorySeparatorChar;
        string parentWithSlash = parent + Path.DirectorySeparatorChar;
        return childWithSlash.StartsWith(parentWithSlash, StringComparison.OrdinalIgnoreCase);
    }
}
