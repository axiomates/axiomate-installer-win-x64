using System;
using System.IO;

namespace AxiomateInstaller.Services;

public sealed record PathEvaluation(bool Ok, string? Reason);

/// <summary>
/// Validates user-supplied install paths to keep the installer from
/// nuking system folders, the user's home, or random disk roots.
///
/// We are about to "force-empty" the target dir, so an aggressive blacklist
/// is critical even though the user already confirmed the wipe.
/// </summary>
public static class DirGuard
{
    public static PathEvaluation Evaluate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new(false, Strings.Get("DG_Empty"));

        string p;
        try
        {
            p = Path.GetFullPath(raw.Trim().TrimEnd('\\', '/'));
        }
        catch (Exception ex)
        {
            return new(false, Strings.Format("DG_Unparseable_Format", ex.Message));
        }

        if (p.Length < 4) // e.g. "C:\"
            return new(false, Strings.Get("DG_Root"));

        if (!Path.IsPathRooted(p) || p.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            return new(false, Strings.Get("DG_Invalid"));

        // Drive root form "X:\"
        var root = Path.GetPathRoot(p);
        if (root != null && string.Equals(p.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return new(false, Strings.Get("DG_Root"));

        string userProfile = UserProfileResolver.GetUserProfile();

        var forbiddenExact = new[]
        {
            userProfile,
            Path.Combine(userProfile, "Desktop"),
            Path.Combine(userProfile, "Documents"),
            Path.Combine(userProfile, "Downloads"),
            Path.Combine(userProfile, "AppData", "Local"),
            Path.Combine(userProfile, "AppData", "Roaming"),
            Path.GetTempPath().TrimEnd('\\'),
        };
        string canonicalPath = PathCanonicalizer.CanonicalizeForComparison(p);
        foreach (var f in forbiddenExact)
        {
            if (string.IsNullOrEmpty(f)) continue;
            string canonicalForbidden = PathCanonicalizer.CanonicalizeForComparison(f);
            if (string.Equals(canonicalPath, canonicalForbidden, StringComparison.OrdinalIgnoreCase))
                return new(false, Strings.Format("DG_Reserved_Format", f));
        }

        var protectedRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        };
        foreach (var f in protectedRoots)
        {
            if (string.IsNullOrEmpty(f)) continue;
            string canonicalRoot = PathCanonicalizer.CanonicalizeForComparison(f);
            if (IsSameOrChild(canonicalPath, canonicalRoot))
                return new(false, Strings.Format("DG_Protected_Format", f));
        }

        // Path must be a child of *something* (avoid "C:\")
        if (Path.GetFileName(p).Length == 0)
            return new(false, Strings.Get("DG_NoLeaf"));

        return new(true, null);
    }

    private static bool IsSameOrChild(string path, string root)
    {
        string normalizedPath = path.TrimEnd('\\');
        string normalizedRoot = root.TrimEnd('\\');
        return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(normalizedRoot + "\\", StringComparison.OrdinalIgnoreCase);
    }

    public static bool DirectoryHasContent(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return false;
            using var enumerator = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
            return enumerator.MoveNext();
        }
        catch { return false; }
    }
}
