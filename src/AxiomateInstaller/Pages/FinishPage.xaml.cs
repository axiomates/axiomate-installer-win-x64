using System.Diagnostics;
using System.IO;
using System.Windows;

namespace AxiomateInstaller.Pages;

public partial class FinishPage : WizardPage
{
    public override string HeaderSubtitle => "安装完成";
    public override bool AllowBack   => false;
    public override bool AllowNext   => false;
    public override bool AllowCancel => false;

    private string _launcherCmd = "";

    public FinishPage() { InitializeComponent(); }

    public override void OnEnter(MainWindow host)
    {
        DetailText.Text = $"Axiomate 已安装到 {host.Options.InstallDir}。系统 PATH 已更新。";

        if (host.Options.CreateWorkspace)
        {
            _launcherCmd = Path.Combine(host.Options.WorkspaceDir, "launch-axiomate.cmd");
            HintText.Text =
                "桌面与开始菜单已创建 Axiomate 快捷方式。" +
                "双击即可在工作区目录打开 Axiomate TUI。\n" +
                "若快捷方式不可用, 可在新终端中输入 axiomate 启动。";
            LaunchBtn.IsEnabled = File.Exists(_launcherCmd);
        }
        else
        {
            HintText.Text =
                "请重新打开终端窗口让 PATH 生效, 然后输入 axiomate 即可启动。\n" +
                "首次启动时, axiomate 将自动生成模型路由配置。";
            LaunchBtn.IsEnabled = false;
            LaunchBtn.Visibility = Visibility.Collapsed;
        }
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var psi = new ProcessStartInfo(_launcherCmd) { UseShellExecute = true };
            Process.Start(psi);
            ((MainWindow)Window.GetWindow(this)!).Close();
        }
        catch
        {
            // ignore
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        ((MainWindow)Window.GetWindow(this)!).Close();
    }
}
