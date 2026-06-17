using System;
using System.IO;

namespace AxiomateInstaller.Services;

/// <summary>
/// Creates the user's chosen workspace directory plus a launcher .cmd
/// that cd's there and runs axiomate. Shortcuts point at this .cmd so
/// double-clicking opens axiomate with workspace as cwd.
/// </summary>
public sealed class WorkspaceCreator
{
    private readonly Logger _log;
    public WorkspaceCreator(Logger log) { _log = log; }

    public string CreateWorkspaceLauncher(string workspaceDir)
    {
        Directory.CreateDirectory(workspaceDir);
        string cmd = Path.Combine(workspaceDir, "launch-axiomate.cmd");
        string content =
            "@echo off\r\n" +
            $"cd /D \"{workspaceDir}\"\r\n" +
            "axiomate %*\r\n";
        File.WriteAllText(cmd, content);
        _log.Info($"Wrote workspace launcher: {cmd}");
        return cmd;
    }
}
