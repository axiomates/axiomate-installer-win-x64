using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace AxiomateInstaller.Services;

public static class PathCanonicalizer
{
    public static string Normalize(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static string CanonicalizeForComparison(string path)
    {
        string normalized = Normalize(path);
        string? existing = FindExistingAncestor(normalized, out string relativeRemainder);
        if (existing is null) return normalized;

        string finalExisting = TryGetFinalPath(existing) ?? Normalize(existing);
        return string.IsNullOrEmpty(relativeRemainder)
            ? finalExisting
            : Normalize(Path.Combine(finalExisting, relativeRemainder));
    }

    private static string? FindExistingAncestor(string path, out string relativeRemainder)
    {
        relativeRemainder = "";
        var missing = new Stack<string>();
        string? current = path;
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.Exists(current) || File.Exists(current))
            {
                relativeRemainder = string.Join(Path.DirectorySeparatorChar, missing);
                return current;
            }

            string? name = Path.GetFileName(current);
            if (!string.IsNullOrEmpty(name)) missing.Push(name);
            string? parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent;
        }
        return null;
    }

    private static string? TryGetFinalPath(string path)
    {
        try
        {
            using SafeFileHandle handle = CreateFileW(
                path,
                0,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero);

            if (handle.IsInvalid) return null;

            var sb = new StringBuilder(512);
            uint len = GetFinalPathNameByHandleW(handle, sb, (uint)sb.Capacity, FILE_NAME_NORMALIZED | VOLUME_NAME_DOS);
            if (len == 0) return null;
            if (len > sb.Capacity)
            {
                sb.EnsureCapacity((int)len);
                len = GetFinalPathNameByHandleW(handle, sb, (uint)sb.Capacity, FILE_NAME_NORMALIZED | VOLUME_NAME_DOS);
                if (len == 0) return null;
            }

            string final = sb.ToString();
            const string prefix = @"\\?\";
            return final.StartsWith(prefix, StringComparison.Ordinal) ? final[prefix.Length..] : final;
        }
        catch
        {
            return null;
        }
    }

    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint FILE_SHARE_DELETE = 0x00000004;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private const uint FILE_NAME_NORMALIZED = 0x0;
    private const uint VOLUME_NAME_DOS = 0x0;

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
}
