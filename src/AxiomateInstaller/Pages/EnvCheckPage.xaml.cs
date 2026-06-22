using System.Threading.Tasks;
using System.Windows;
using AxiomateInstaller.Services;

namespace AxiomateInstaller.Pages;

public partial class EnvCheckPage : WizardPage
{
    private bool _checking;

    public override string HeaderSubtitleKey => "Env_PageSubtitle";
    public override bool AllowBack => !_checking;
    public override bool AllowNext => !_checking;

    public EnvCheckPage() { InitializeComponent(); }

    public override async void OnEnter(MainWindow host)
    {
        _checking = true;
        host.NextBtn.IsEnabled = false;
        host.LastEnvCheck = null;
        OsText.Text = ArchText.Text = WtText.Text = GitText.Text = PyText.Text = Strings.Get("Env_Detecting");
        OsBadge.Text = ArchBadge.Text = "";
        var checker = new EnvironmentChecker(host.Log);
        var result = await checker.RunAsync();
        host.LastEnvCheck = result;

        OsText.Text = result.OsDisplay;
        OsBadge.Text = result.IsSupportedOs ? "✓" : "✗";
        OsBadge.Foreground = result.IsSupportedOs ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Crimson;

        ArchText.Text = result.ArchDisplay;
        ArchBadge.Text = result.IsX64 ? "✓" : "✗";
        ArchBadge.Foreground = result.IsX64 ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Crimson;

        WtRow.Visibility = result.IsWindows10 ? Visibility.Visible : Visibility.Collapsed;
        WtText.Text = result.WindowsTerminalDisplay ?? Strings.Get("Env_WtNotFound");
        if (result.IsWindows10 && result.WindowsTerminalOk)
        {
            WtInstallChk.Visibility = Visibility.Collapsed;
            WtOkBadge.Visibility    = Visibility.Visible;
            WtInstallChk.IsChecked  = false;
        }
        else if (result.IsWindows10)
        {
            WtInstallChk.Visibility = Visibility.Visible;
            WtInstallChk.Content    = Strings.Get("Env_WtInstallRequired");
            WtOkBadge.Visibility    = Visibility.Collapsed;
            WtInstallChk.IsChecked  = true;
            WtInstallChk.IsEnabled  = false;
        }

        GitText.Text = result.GitVersionDisplay ?? Strings.Get("Env_GitNotFound");
        if (result.GitOk)
        {
            // Git already meets the bar — show a green tick instead of an unchecked
            // greyed-out box, which previously read as "you forgot to tick it".
            GitInstallChk.Visibility = System.Windows.Visibility.Collapsed;
            GitOkBadge.Visibility    = System.Windows.Visibility.Visible;
            GitInstallChk.IsChecked  = false;
        }
        else
        {
            // Git is required by axiomate so the bundled install isn't optional.
            // The box stays checked and locked — opting out would dead-end the wizard.
            GitInstallChk.Visibility = System.Windows.Visibility.Visible;
            GitInstallChk.Content    = Strings.Get("Env_GitInstallRequired");
            GitOkBadge.Visibility    = System.Windows.Visibility.Collapsed;
            GitInstallChk.IsChecked  = true;
            GitInstallChk.IsEnabled  = false;
        }

        PyText.Text = result.PythonVersionDisplay ?? Strings.Get("Env_PyNotFound");
        if (result.PythonOk)
        {
            PyInstallChk.Visibility = System.Windows.Visibility.Collapsed;
            PyOkBadge.Visibility    = System.Windows.Visibility.Visible;
            PyInstallChk.IsChecked  = false;
        }
        else
        {
            // Python is optional. Pre-check the box but let the user opt out — many
            // axiomate workflows don't need Python at all.
            PyInstallChk.Visibility = System.Windows.Visibility.Visible;
            PyInstallChk.Content    = Strings.Get("Env_PyInstall");
            PyOkBadge.Visibility    = System.Windows.Visibility.Collapsed;
            PyInstallChk.IsChecked  = true;
            PyInstallChk.IsEnabled  = true;
        }

        _checking = false;
        host.UpdateChrome(this);
        UpdateMirrorVisibility();
        UpdateWarning(host);
    }

    private void PyInstallChk_Toggle(object sender, RoutedEventArgs e) => UpdateMirrorVisibility();

    private void MirrorEnable_Toggle(object sender, RoutedEventArgs e)
    {
        if (MirrorOptionsPanel != null)
            MirrorOptionsPanel.IsEnabled = MirrorEnableChk.IsChecked == true;
    }

    private void UpdateMirrorVisibility()
    {
        MirrorBlock.Visibility = (PyInstallChk.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateWarning(MainWindow host)
    {
        if (host.LastEnvCheck is null) return;
        var r = host.LastEnvCheck;
        if (!r.IsSupportedOs)
        {
            WarningText.Text = Strings.Get("Env_Warn_NeedWin10");
            host.NextBtn.IsEnabled = false;
            return;
        }
        if (!r.IsX64)
        {
            WarningText.Text = Strings.Get("Env_Warn_NeedX64");
            host.NextBtn.IsEnabled = false;
            return;
        }
        WarningText.Text = r.NeedsWindowsTerminal ? Strings.Get("Env_Warn_WtRequired") : "";
        host.NextBtn.IsEnabled = true;
    }

    public override bool Validate(MainWindow host)
    {
        if (host.LastEnvCheck is null) return false;
        var r = host.LastEnvCheck;
        if (!r.IsSupportedOs || !r.IsX64) return false;

        if (r.NeedsWindowsTerminal && WtInstallChk.IsChecked != true)
        {
            MessageBox.Show(Strings.Get("Env_Block_WtBody"), Strings.Get("Env_Block_WtTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // Git is required: if missing, GitInstallChk is locked-checked so this branch
        // is defensive; we never expect to hit it. Python is opt-in — no validation
        // when the user unchecks it.
        if (!r.GitOk && GitInstallChk.IsChecked != true)
        {
            MessageBox.Show(Strings.Get("Env_Block_GitBody"), Strings.Get("Env_Block_GitTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    public override async Task<bool> BeforeNextAsync(MainWindow host)
    {
        if (host.LastEnvCheck?.NeedsWindowsTerminal != true) return true;

        _checking = true;
        host.UpdateChrome(this);
        host.NextBtn.IsEnabled = false;
        WarningText.Text = Strings.Get("Env_WtInstalling");

        try
        {
            var runner = new InstallerRunner(host.Log);
            await runner.RunWindowsTerminalAsync();

            var checker = new EnvironmentChecker(host.Log);
            host.LastEnvCheck = await checker.RunAsync();
            if (!host.LastEnvCheck.WindowsTerminalOk)
            {
                MessageBox.Show(Strings.Get("Env_Block_WtAfterInstallBody"), Strings.Get("Env_Block_WtTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            WtText.Text = host.LastEnvCheck.WindowsTerminalDisplay ?? "Windows Terminal";
            WtInstallChk.Visibility = Visibility.Collapsed;
            WtOkBadge.Visibility    = Visibility.Visible;
            WarningText.Text = "";
            return true;
        }
        catch (InstallStepException ex)
        {
            MessageBox.Show(ex.Message, Strings.Get("Env_Block_WtTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally
        {
            _checking = false;
            host.UpdateChrome(this);
            UpdateWarning(host);
        }
    }

    public override void OnLeave(MainWindow host)
    {
        host.Options.NeedsGitInstall    = GitInstallChk.IsChecked == true && host.LastEnvCheck?.GitOk == false;
        host.Options.NeedsPythonInstall = PyInstallChk.IsChecked == true && host.LastEnvCheck?.PythonOk == false;
        host.Options.ConfigurePipMirror = host.Options.NeedsPythonInstall && MirrorEnableChk.IsChecked == true;
        host.Options.PipMirrorChoice    = MirrorTsinghua.IsChecked == true ? PipMirror.Tsinghua : PipMirror.Aliyun;
    }
}
