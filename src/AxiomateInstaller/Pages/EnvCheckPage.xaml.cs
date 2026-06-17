using System.Windows;
using AxiomateInstaller.Services;

namespace AxiomateInstaller.Pages;

public partial class EnvCheckPage : WizardPage
{
    public override string HeaderSubtitle => "环境检查";

    public EnvCheckPage() { InitializeComponent(); }

    public override async void OnEnter(MainWindow host)
    {
        OsText.Text = ArchText.Text = GitText.Text = PyText.Text = "正在检测...";
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

        GitText.Text = result.GitVersionDisplay ?? "未检测到 Git";
        GitInstallChk.IsChecked = !result.GitOk;
        GitInstallChk.IsEnabled = !result.GitOk;
        if (result.GitOk) GitInstallChk.Content = $"已安装 ({result.GitVersionDisplay})";

        PyText.Text = result.PythonVersionDisplay ?? "未检测到 Python";
        PyInstallChk.IsChecked = !result.PythonOk;
        PyInstallChk.IsEnabled = !result.PythonOk;
        if (result.PythonOk) PyInstallChk.Content = $"已安装 ({result.PythonVersionDisplay})";

        UpdateMirrorVisibility();
        UpdateWarning(host);
    }

    private void PyInstallChk_Toggle(object sender, RoutedEventArgs e)
    {
        UpdateMirrorVisibility();
    }

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
            WarningText.Text = "需要 Windows 11 (build 22000) 或更高版本。无法继续。";
            host.NextBtn.IsEnabled = false;
            return;
        }
        if (!r.IsX64)
        {
            WarningText.Text = "需要 64 位 (x64) Windows。无法继续。";
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

        // If user unchecked auto-install but Git/Python are missing, block.
        if (!r.GitOk && GitInstallChk.IsChecked != true)
        {
            MessageBox.Show("Git 未达到要求，且您取消了内置 Git 安装。请勾选或先自行安装。",
                "环境检查", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (!r.PythonOk && PyInstallChk.IsChecked != true)
        {
            MessageBox.Show("Python 未达到要求，且您取消了内置 Python 安装。请勾选或先自行安装。",
                "环境检查", MessageBoxButton.OK, MessageBoxImage.Warning);
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
