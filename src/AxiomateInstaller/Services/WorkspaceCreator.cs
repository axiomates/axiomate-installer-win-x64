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

    public string CreateWorkspaceLauncher(string installDir, string workspaceDir)
    {
        Directory.CreateDirectory(installDir);
        string cmd = Path.Combine(installDir, "launch-axiomate.cmd");
        string content =
            "@echo off\r\n" +
            $"set \"AXIOMATE_WORKSPACE={workspaceDir}\"\r\n" +
            "if not exist \"%AXIOMATE_WORKSPACE%\" mkdir \"%AXIOMATE_WORKSPACE%\"\r\n" +
            "cd /D \"%AXIOMATE_WORKSPACE%\"\r\n" +
            "axiomate \"%AXIOMATE_WORKSPACE%\" %*\r\n";
        File.WriteAllText(cmd, content);
        _log.Info($"Wrote workspace launcher: {cmd} -> {workspaceDir}");
        return cmd;
    }
}
