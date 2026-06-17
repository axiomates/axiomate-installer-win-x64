using System;
using System.IO;
using System.Threading.Tasks;

namespace AxiomateInstaller.Services;

/// <summary>
/// Orchestrates the install pipeline. Reports progress via the supplied
/// IProgress so the UI can update without coupling to WPF here.
/// </summary>
public sealed class InstallEngine
{
    private readonly Logger _log;
    private readonly VersionInfo _version;

    public InstallEngine(Logger log, VersionInfo version)
    {
        _log = log;
        _version = version;
    }

    public async Task RunAsync(InstallOptions opt, IProgress<InstallProgress> progress)
    {
        PayloadExtractor.ExtractedPayload? payload = null;
        var extractor = new PayloadExtractor(_log);
        var runner    = new InstallerRunner(_log);
        var deployer  = new AxiomateDeployer(_log);
        var pathReg   = new PathRegistrar(_log);
        var pipMirror = new PipMirrorWriter(_log);
        var workspace = new WorkspaceCreator(_log);
        var shortcut  = new ShortcutManager(_log);
        var configW   = new ConfigWriter(_log);
        var uninstReg = new UninstallRegistrar(_log);

        int totalSteps = ComputeStepCount(opt);
        int step = 0;
        void Report(string title) => progress.Report(new InstallProgress(++step, totalSteps, title));

        try
        {
            Report("正在解压安装资源");
            payload = extractor.Extract();

            if (opt.NeedsGitInstall)
            {
                Report("正在安装 Git for Windows");
                await runner.RunGitAsync(payload.GitInstaller);
            }

            if (opt.NeedsPythonInstall)
            {
                Report("正在安装 Python 3.12");
                await runner.RunPythonAsync(payload.PythonInstaller, opt.PythonInstallDir);

                if (opt.ConfigurePipMirror)
                {
                    Report("配置 pip 镜像源");
                    pipMirror.Write(opt.PythonInstallDir, opt.PipMirrorChoice);
                }
            }

            Report("部署 Axiomate 到目标目录");
            DeployManifest manifest = await deployer.DeployAsync(
                payload.DistDir, opt.InstallDir, _version.AxiomateVersion, _version.InstallerVersion);

            Report("注册系统 PATH");
            pathReg.EnsureInPath(opt.InstallDir);

            if (opt.QuickModelConfig)
            {
                Report("写入 Axiomate 模型配置");
                configW.Write(opt.ModelChoice, opt.ApiKey);
            }

            string? launcherCmd = null;
            if (opt.CreateWorkspace)
            {
                Report("创建工作区与快捷方式");
                launcherCmd = workspace.CreateWorkspaceLauncher(opt.WorkspaceDir);
                CreateShortcuts(shortcut, opt, launcherCmd);
            }

            Report("注册卸载器");
            uninstReg.Register(
                opt.InstallDir,
                payload.UninstallerExe,
                _version.AxiomateVersion,
                _version.InstallerVersion,
                manifest.TotalBytes);
        }
        finally
        {
            if (payload is not null)
                PayloadExtractor.Cleanup(payload.WorkDir, _log);
        }
    }

    private static int ComputeStepCount(InstallOptions opt)
    {
        int n = 1; // extract
        if (opt.NeedsGitInstall) n++;
        if (opt.NeedsPythonInstall)
        {
            n++;
            if (opt.ConfigurePipMirror) n++;
        }
        n += 2; // deploy + path
        if (opt.QuickModelConfig) n++;
        if (opt.CreateWorkspace) n++;
        n += 1; // uninstall registrar
        return n;
    }

    private void CreateShortcuts(ShortcutManager sm, InstallOptions opt, string launcherCmd)
    {
        string axiomateExe = Path.Combine(opt.InstallDir, "axiomate.exe");
        string description = "Axiomate AI agent CLI";

        // Per-machine shortcuts.
        string startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            "Programs");
        string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

        sm.Create(
            Path.Combine(startMenuDir, "Axiomate.lnk"),
            launcherCmd,
            opt.WorkspaceDir,
            description,
            axiomateExe);

        sm.Create(
            Path.Combine(desktopDir, "Axiomate.lnk"),
            launcherCmd,
            opt.WorkspaceDir,
            description,
            axiomateExe);
    }
}

public sealed record InstallProgress(int CurrentStep, int TotalSteps, string Title);
