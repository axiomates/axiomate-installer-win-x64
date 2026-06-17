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
        var winTuner  = new WindowsSettingsTuner(_log);
        var workspace = new WorkspaceCreator(_log);
        var shortcut  = new ShortcutManager(_log);
        var configW   = new ConfigWriter(_log);
        var uninstReg = new UninstallRegistrar(_log);

        int totalSteps = ComputeStepCount(opt);
        int step = 0;
        void Report(string title) => progress.Report(new InstallProgress(++step, totalSteps, title));

        try
        {
            ValidateFinalOptions(opt);

            Report("Step_Extract");
            payload = extractor.Extract();

            if (opt.NeedsGitInstall)
            {
                Report("Step_GitInstall");
                await runner.RunGitAsync(payload.GitInstaller);
            }

            if (opt.NeedsPythonInstall)
            {
                Report("Step_PyInstall");
                await runner.RunPythonAsync(payload.PythonInstaller, opt.PythonInstallDir);

                if (opt.ConfigurePipMirror)
                {
                    Report("Step_PipMirror");
                    pipMirror.Write(opt.PythonInstallDir, opt.PipMirrorChoice);
                }
            }

            Report("Step_Deploy");
            DeployManifest manifest = await deployer.DeployAsync(
                payload.DistDir, opt.InstallDir, _version.AxiomateVersion, _version.InstallerVersion);

            Report("Step_Path");
            pathReg.EnsureInPath(opt.InstallDir);

            Report("Step_WinTune");
            await winTuner.ApplyAsync();

            if (opt.QuickModelConfig)
            {
                Report("Step_Model");
                configW.Write(opt.ModelChoice, opt.ApiKey);
            }

            string? launcherCmd = null;
            if (opt.CreateWorkspace)
            {
                Report("Step_Workspace");
                launcherCmd = workspace.CreateWorkspaceLauncher(opt.InstallDir, opt.WorkspaceDir);
                CreateShortcuts(shortcut, opt, launcherCmd);
            }

            Report("Step_Uninst");
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

    private void ValidateFinalOptions(InstallOptions opt)
    {
        if (!opt.CreateWorkspace) return;
        string userProfile = UserProfileResolver.GetUserProfile(_log);
        WorkspacePathGuard.EnsureSeparateFromInstallDir(opt.WorkspaceDir, opt.InstallDir, userProfile);
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
        n += 3; // deploy + path + win-tune
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
            opt.InstallDir,
            description,
            axiomateExe);

        sm.Create(
            Path.Combine(desktopDir, "Axiomate.lnk"),
            launcherCmd,
            opt.InstallDir,
            description,
            axiomateExe);
    }
}

public sealed record InstallProgress(int CurrentStep, int TotalSteps, string TitleKey)
{
    public string Title => Strings.Get(TitleKey);

    /// <summary>The localized title resolved at call time so it always matches the active language.</summary>
    public string LocalizedTitle() => Strings.Get(TitleKey);
}
