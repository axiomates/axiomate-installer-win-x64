namespace AxiomateInstaller.Pages;

public partial class WelcomePage : WizardPage
{
    public override string HeaderSubtitle => "欢迎";
    public override bool AllowBack => false;

    public WelcomePage() { InitializeComponent(); }

    public override void OnEnter(MainWindow host)
    {
        SubtitleText.Text =
            $"安装器版本 {host.Version.InstallerVersion} · 包含 Axiomate {host.Version.AxiomateVersion}。" +
            "下一步将检测您的运行环境。";
    }
}
