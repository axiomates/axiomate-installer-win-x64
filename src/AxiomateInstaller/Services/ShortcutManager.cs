using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AxiomateInstaller.Services;

/// <summary>
/// Creates Windows .lnk files via the IShellLink COM interface.
/// We dynamically use COM to avoid the WindowsScriptHost dependency.
/// </summary>
public sealed class ShortcutManager
{
    private readonly Logger _log;
    public ShortcutManager(Logger log) { _log = log; }

    public void Create(
        string lnkPath,
        string targetPath,
        string workingDirectory,
        string description,
        string? iconLocation = null,
        int iconIndex = 0)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lnkPath)!);

        Type? shellLinkType = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"));
        if (shellLinkType is null) throw new InstallStepException(Strings.Get("Err_Lnk_NoCom"));
        object? shellLink = Activator.CreateInstance(shellLinkType);
        if (shellLink is null) throw new InstallStepException(Strings.Get("Err_Lnk_NoInst"));

        try
        {
            var sl = (IShellLinkW)shellLink;
            sl.SetPath(targetPath);
            sl.SetWorkingDirectory(workingDirectory);
            sl.SetDescription(description);
            if (!string.IsNullOrEmpty(iconLocation))
                sl.SetIconLocation(iconLocation, iconIndex);

            var pf = (IPersistFile)shellLink;
            pf.Save(lnkPath, true);
            _log.Info($"Shortcut created: {lnkPath} -> {targetPath}");
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellLink);
        }
    }

    public void Delete(string lnkPath)
    {
        try
        {
            if (File.Exists(lnkPath))
            {
                File.Delete(lnkPath);
                _log.Info($"Shortcut removed: {lnkPath}");
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Could not delete shortcut {lnkPath}: {ex.Message}");
        }
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile,
                     int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotKey(out short pwHotkey);
        void SetHotKey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath,
                             int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, Guid("0000010b-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName,
                  [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
