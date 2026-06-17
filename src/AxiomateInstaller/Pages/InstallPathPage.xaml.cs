using System.IO;
using System.Windows;
using AxiomateInstaller.Services;

namespace AxiomateInstaller.Pages;

public partial class InstallPathPage : WizardPage
{
    public override string HeaderSubtitle => "选择安装位置";

    public InstallPathPage() { InitializeComponent(); }

    public override void OnEnter(MainWindow host)
    {
        PathBox.Text = host.Options.InstallDir;
        UpdateHint();
        PathBox.TextChanged += (_, _) => UpdateHint();
    }

    private void UpdateHint()
    {
        var eval = DirGuard.Evaluate(PathBox.Text);
        if (!eval.Ok)
        {
            WarnText.Text = eval.Reason ?? "";
            HintText.Text = "";
            return;
        }
        WarnText.Text = "";
        HintText.Text = DirGuard.DirectoryHasContent(PathBox.Text)
            ? "提示: 该目录非空，安装时会被清空。"
            : "目录可用。";
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择 Axiomate 安装目录",
            InitialDirectory = Directory.Exists(PathBox.Text) ? PathBox.Text : @"C:\Program Files"
        };
        if (dlg.ShowDialog() == true)
        {
            PathBox.Text = dlg.FolderName;
        }
    }

    public override bool Validate(MainWindow host)
    {
        var eval = DirGuard.Evaluate(PathBox.Text);
        if (!eval.Ok)
        {
            MessageBox.Show(eval.Reason, "目录不可用", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (DirGuard.DirectoryHasContent(PathBox.Text))
        {
            var res = MessageBox.Show(
                $"目录 \"{PathBox.Text}\" 非空。\n安装器将清空该目录后再写入。继续？",
                "确认清空",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);
            if (res != MessageBoxResult.OK) return false;
        }
        return true;
    }

    public override void OnLeave(MainWindow host)
    {
        host.Options.InstallDir = PathBox.Text.Trim().TrimEnd('\\');
    }
}
