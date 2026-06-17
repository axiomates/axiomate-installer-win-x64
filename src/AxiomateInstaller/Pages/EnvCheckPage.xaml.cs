using System.Windows;
using AxiomateInstaller.Services;

namespace AxiomateInstaller.Pages;

public partial class EnvCheckPage : WizardPage
{
    public override string HeaderSubtitleKey => "Env_PageSubtitle";

    public EnvCheckPage() { InitializeComponent(); }

    public override async void OnEnter(MainWindow host)
    {
        OsText.Text = ArchText.Text = GitText.Text = PyText.Text = Strings.Get("Env_Detecting");
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

        GitText.Text = result.GitVersionDisplay ?? Strings.Get("Env_GitNotFound");
        GitInstallChk.IsChecked = !result.GitOk;
        GitInstallChk.IsEnabled = !result.GitOk;
        if (result.GitOk) GitInstallChk.Content = Strings.Format("Env_GitInstalled_Format", result.GitVersionDisplay ?? "");

        PyText.Text = result.PythonVersionDisplay ?? Strings.Get("Env_PyNotFound");
        PyInstallChk.IsChecked = !result.PythonOk;
        PyInstallChk.IsEnabled = !result.PythonOk;
        if (result.PythonOk) PyInstallChk.Content = Strings.Format("Env_PyInstalled_Format", result.PythonVersionDisplay ?? "");

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
            WarningText.Text = Strings.Get("Env_Warn_NeedWin11");
            host.NextBtn.IsEnabled = false;
            return;
        }
        if (!r.IsX64)
        {
            WarningText.Text = Strings.Get("Env_Warn_NeedX64");
            host.NextBtn.IsEnabled = false;
            return;
        }
        WarningText.Text = "";
        host.NextBtn.IsEnabled = true;
    }

    public override bool Validate(MainWindow host)
    {
        if (host.LastEnvCheck is null) return false;
        var r = host.LastEnvCheck;
        if (!r.IsSupportedOs || !r.IsX64) return false;

        if (!r.GitOk && GitInstallChk.IsChecked != true)
        {
            MessageBox.Show(Strings.Get("Env_Block_GitBody"), Strings.Get("Env_Block_GitTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (!r.PythonOk && PyInstallChk.IsChecked != true)
        {
            MessageBox.Show(Strings.Get("Env_Block_PyBody"), Strings.Get("Env_Block_PyTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    public override void OnLeave(MainWindow host)
    {
        host.Options.NeedsGitInstall    = GitInstallChk.IsChecked == true && host.LastEnvCheck?.GitOk == false;
        host.Options.NeedsPythonInstall = PyInstallChk.IsChecked == true && host.LastEnvCheck?.PythonOk == false;
        host.Options.ConfigurePipMirror = host.Options.NeedsPythonInstall && MirrorEnableChk.IsChecked == true;
        host.Options.PipMirrorChoice    = MirrorTsinghua.IsChecked == true ? PipMirror.Tsinghua : PipMirror.Aliyun;
    }
}
