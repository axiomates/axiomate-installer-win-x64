using System;
using System.IO;
using System.Windows;
using AxiomateInstaller.Services;

namespace AxiomateInstaller.Pages;

public partial class OptionsPage : WizardPage
{
    public override string HeaderSubtitleKey => "Opt_PageSubtitle";

    public OptionsPage() { InitializeComponent(); }

    public override void OnEnter(MainWindow host)
    {
        ModelChk.IsChecked      = host.Options.QuickModelConfig;
        BypassPermChk.IsChecked = host.Options.EnableBypassPermissions;
        WorkspaceChk.IsChecked  = host.Options.CreateWorkspace;
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
        string userProfile = UserProfileResolver.GetUserProfile();
        string resolvedWorkspace = WorkspacePathGuard.ResolveForUser(WorkspacePathBox.Text, userProfile);
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Strings.Get("Opt_PickerTitle"),
            InitialDirectory = Directory.Exists(resolvedWorkspace)
                ? resolvedWorkspace
                : userProfile
        };
        if (dlg.ShowDialog() == true) WorkspacePathBox.Text = dlg.FolderName;
    }

    public override bool Validate(MainWindow host)
    {
        if (WorkspaceChk.IsChecked != true) return true;
        string p = WorkspacePathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(p))
        {
            MessageBox.Show(Strings.Get("Opt_BadWorkspaceBody_Empty"), Strings.Get("Opt_BadWorkspaceTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        try
        {
            string userProfile = UserProfileResolver.GetUserProfile(host.Log);
            _ = WorkspacePathGuard.ResolveForUser(p, userProfile);
            WorkspacePathGuard.EnsureSeparateFromInstallDir(p, host.Options.InstallDir, userProfile);
        }
        catch (InstallStepException ex)
        {
            MessageBox.Show(ex.Message, Strings.Get("Opt_BadWorkspaceTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        catch
        {
            MessageBox.Show(Strings.Get("Opt_BadWorkspaceBody_Invalid"), Strings.Get("Opt_BadWorkspaceTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    public override void OnLeave(MainWindow host)
    {
        host.Options.QuickModelConfig = ModelChk.IsChecked == true;
        host.Options.EnableBypassPermissions = BypassPermChk.IsChecked == true;
        host.Options.CreateWorkspace  = WorkspaceChk.IsChecked == true;
        host.Options.WorkspaceDir     = WorkspacePathBox.Text.Trim().TrimEnd('\\');
    }

    public override int NextIndex(MainWindow host, int current)
    {
        return host.Options.QuickModelConfig ? current + 1 : current + 2;
    }
}
