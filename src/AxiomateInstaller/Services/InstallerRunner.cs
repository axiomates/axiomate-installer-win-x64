using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AxiomateInstaller.Services;

/// <summary>
/// Runs Git / Python silent installers and surfaces friendly errors.
/// </summary>
public sealed class InstallerRunner
{
    private readonly Logger _log;

    public InstallerRunner(Logger log) { _log = log; }

    /// <summary>Inno Setup arguments for Git for Windows 2.54.0 — no internet needed,
    /// PATH includes Unix tools, editor = nano (bundled), components include lfs+scalar.</summary>
    public static string BuildGitArguments()
    {
        var sb = new StringBuilder();
        sb.Append("/VERYSILENT /NORESTART /SP- /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /NOICONS ");
        sb.Append("/COMPONENTS=\"icons,ext\\reg\\shellhere,assoc,assoc_sh,gitlfs,windowsterminal,scalar\" ");
        sb.Append("/o:PathOption=CmdTools ");
        sb.Append("/o:SSHOption=OpenSSH ");
        sb.Append("/o:CURLOption=WinSSL ");
        sb.Append("/o:CRLFOption=CRLFCommitAsIs ");
        sb.Append("/o:BashTerminalOption=ConHost ");
        sb.Append("/o:DefaultBranchOption=main ");
        sb.Append("/o:CredentialManager=Enabled ");
        sb.Append("/o:EditorOption=Nano ");
        sb.Append("/o:EnableSymlinks=Disabled ");
        sb.Append("/o:EnableFSMonitor=Disabled");
        return sb.ToString();
    }

    public static string BuildPythonArguments(string targetDir)
    {
        // Note: TargetDir is wrapped in quotes if it contains spaces; the Python
        // installer expects "key=value" pairs separated by spaces, with values
        // optionally double-quoted.
        return string.Join(' ',
            "/quiet",
            "InstallAllUsers=1",
            $"TargetDir=\"{targetDir}\"",
            "PrependPath=1",
            "Include_launcher=1",
            "AssociateFiles=1",
            "Include_pip=1",
            "Include_tcltk=1",
            "Include_test=0",
            "Include_doc=0",
            "Include_dev=0",
            "Include_debug=0",
            "Shortcuts=1",
            "SimpleInstall=1",
            "SimpleInstallDescription=\"Installing Python 3.12.10\"");
    }

    public async Task RunGitAsync(string installerPath)
    {
        string args = BuildGitArguments();
        _log.Info($"Running Git installer: {Path.GetFileName(installerPath)} {args}");
        int code = await RunAsync(installerPath, args);
        if (code is 0 or 3010)  // 3010 = success, restart required
        {
            _log.Info("Git installer succeeded.");
            return;
        }
        throw new InstallStepException($"Git 安装失败（退出码 {code}）。请检查日志了解详情。");
    }

    public async Task RunPythonAsync(string installerPath, string targetDir)
    {
        string args = BuildPythonArguments(targetDir);
        _log.Info($"Running Python installer: {Path.GetFileName(installerPath)} {args}");
        int code = await RunAsync(installerPath, args);
        switch (code)
        {
            case 0:
                _log.Info("Python installer succeeded.");
                return;
            case 1602:
                throw new InstallStepException("用户取消了 Python 安装。");
            case 1603:
                throw new InstallStepException("Python 安装时遇到致命错误（退出码 1603）。可能因为目标目录被占用、磁盘空间不足或权限问题。");
            default:
                throw new InstallStepException($"Python 安装失败（退出码 {code}）。请检查日志了解详情。");
        }
    }

    private async Task<int> RunAsync(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi)
            ?? throw new InstallStepException($"无法启动进程：{exe}");

        var so = p.StandardOutput.ReadToEndAsync();
        var se = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();

        string stdout = await so;
        string stderr = await se;
        if (!string.IsNullOrWhiteSpace(stdout)) _log.Info("[stdout] " + stdout.Trim());
        if (!string.IsNullOrWhiteSpace(stderr)) _log.Info("[stderr] " + stderr.Trim());
        return p.ExitCode;
    }
}

public sealed class InstallStepException : Exception
{
    public InstallStepException(string message) : base(message) { }
    public InstallStepException(string message, Exception inner) : base(message, inner) { }
}
