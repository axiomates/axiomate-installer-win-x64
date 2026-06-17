using System;
using System.IO;
using System.Windows;

namespace AxiomateInstaller.Pages;

public partial class OptionsPage : WizardPage
{
    public override string HeaderSubtitle => "安装选项";

    public OptionsPage() { InitializeComponent(); }

    public override void OnEnter(MainWindow host)
    {
        ModelChk.IsChecked     = host.Options.QuickModelConfig;
        WorkspaceChk.IsChecked = host.Options.CreateWorkspace;
        WorkspacePathBox.Text  = host.Options.WorkspaceDir;
        UpdateWorkspaceEnabled();
    }

    private void WorkspaceChk_Toggle(object sender, RoutedEventArgs e) => UpdateWorkspaceEnabled();

    private void UpdateWorkspaceEnabled()
    {
        if (WorkspacePathGrid != null)
            WorkspacePathGrid.IsEnabled = WorkspaceChk.IsChecked == true;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择 Axiomate 工作区目录",
            InitialDirectory = Directory.Exists(WorkspacePathBox.Text)
                ? WorkspacePathBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (dlg.ShowDialog() == true) WorkspacePathBox.Text = dlg.FolderName;
    }

    public override bool Validate(MainWindow host)
    {
        if (WorkspaceChk.IsChecked != true) return true;
        string p = WorkspacePathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(p))
        {
            MessageBox.Show("请填写工作区目录。", "工作区", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        try { Path.GetFullPath(p); }
        catch { MessageBox.Show("工作区路径不合法。", "工作区", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
        return true;
    }

    public override void OnLeave(MainWindow host)
    {
        host.Options.QuickModelConfig = ModelChk.IsChecked == true;
        host.Options.CreateWorkspace  = WorkspaceChk.IsChecked == true;
        host.Options.WorkspaceDir     = WorkspacePathBox.Text.Trim().TrimEnd('\\');
    }

    public override int NextIndex(MainWindow host, int current)
    {
        // Skip ModelConfigPage when not requested.
        return host.Options.QuickModelConfig ? current + 1 : current + 2;
    }
}
