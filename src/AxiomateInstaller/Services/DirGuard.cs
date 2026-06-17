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
            return new(false, "请填写安装路径。");

        string p;
        try
        {
            p = Path.GetFullPath(raw.Trim().TrimEnd('\\', '/'));
        }
        catch (Exception ex)
        {
            return new(false, $"路径无法解析：{ex.Message}");
        }

        if (p.Length < 4) // e.g. "C:\"
            return new(false, "不能直接安装到驱动器根目录。");

        if (!Path.IsPathRooted(p) || p.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            return new(false, "路径包含非法字符或不是绝对路径。");

        // Drive root form "X:\"
        var root = Path.GetPathRoot(p);
        if (root != null && string.Equals(p.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return new(false, "不能直接安装到驱动器根目录。");

        string userProfile = UserProfileResolver.GetUserProfile();

        // Forbidden exact dirs.
        var forbiddenExact = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
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
                return new(false, $"不能安装到系统/用户保留目录：{f}");
        }

        // Forbidden ancestors (e.g. you may install *under* C:\Program Files but never *as* C:\Program Files).
        // Already covered above for exact match; subdirs are fine.

        // Path must be a child of *something* (avoid "C:\")
        if (Path.GetFileName(p).Length == 0)
            return new(false, "请提供具体的子目录，不要直接装在根目录。");

        return new(true, null);
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
