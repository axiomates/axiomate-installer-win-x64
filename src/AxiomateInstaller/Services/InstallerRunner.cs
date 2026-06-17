using System;
using System.Diagnostics;
using System.IO;
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
    public static string[] BuildGitArguments() => new[]
    {
        "/VERYSILENT",
        "/NORESTART",
        "/SP-",
        "/SUPPRESSMSGBOXES",
        "/CLOSEAPPLICATIONS",
        "/RESTARTAPPLICATIONS",
        "/NOICONS",
        @"/COMPONENTS=icons,ext\reg\shellhere,assoc,assoc_sh,gitlfs,windowsterminal,scalar",
        "/o:PathOption=CmdTools",
        "/o:SSHOption=OpenSSH",
        "/o:CURLOption=WinSSL",
        "/o:CRLFOption=CRLFCommitAsIs",
        "/o:BashTerminalOption=ConHost",
        "/o:DefaultBranchOption=main",
        "/o:CredentialManager=Enabled",
        "/o:EditorOption=Nano",
        "/o:EnableSymlinks=Disabled",
        "/o:EnableFSMonitor=Disabled",
    };

    public static string[] BuildPythonArguments(string targetDir) => new[]
    {
        "/quiet",
        "InstallAllUsers=1",
        $"TargetDir={targetDir}",
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
        "SimpleInstallDescription=Installing Python 3.12.10",
    };

    public async Task RunGitAsync(string installerPath)
    {
        string[] args = BuildGitArguments();
        _log.Info($"Running Git installer: {Path.GetFileName(installerPath)} {string.Join(' ', args)}");
        int code = await RunAsync(installerPath, args);
        if (code is 0 or 3010)  // 3010 = success, restart required
        {
            _log.Info("Git installer succeeded.");
            return;
        }
        throw new InstallStepException(Strings.Format("Err_GitFail_Format", code));
    }

    public async Task RunPythonAsync(string installerPath, string targetDir)
    {
        string[] args = BuildPythonArguments(targetDir);
        _log.Info($"Running Python installer: {Path.GetFileName(installerPath)} {string.Join(' ', args)}");
        int code = await RunAsync(installerPath, args);
        switch (code)
        {
            case 0:
                _log.Info("Python installer succeeded.");
                return;
            case 1602:
                throw new InstallStepException(Strings.Get("Err_PyCancel"));
            case 1603:
                throw new InstallStepException(Strings.Get("Err_PyFatal"));
            default:
                throw new InstallStepException(Strings.Format("Err_PyFail_Format", code));
        }
    }

    private async Task<int> RunAsync(string exe, string[] args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string arg in args) psi.ArgumentList.Add(arg);
        using var p = Process.Start(psi)
            ?? throw new InstallStepException(Strings.Format("Err_StartProc_Format", exe));

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
