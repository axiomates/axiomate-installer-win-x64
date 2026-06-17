using System.Diagnostics;
using System.IO;
using System.Windows;
using AxiomateInstaller.Services;

namespace AxiomateInstaller.Pages;

public partial class FinishPage : WizardPage
{
    public override string HeaderSubtitleKey => "Finish_PageSubtitle";
    public override bool AllowBack   => false;
    public override bool AllowNext   => false;
    public override bool AllowCancel => false;

    private string _launcherCmd = "";

    public FinishPage() { InitializeComponent(); }

    public override void OnEnter(MainWindow host)
    {
        DetailText.Text = Strings.Format("Finish_Detail_Format", host.Options.InstallDir);

        if (host.Options.CreateWorkspace)
        {
            _launcherCmd = Path.Combine(host.Options.WorkspaceDir, "launch-axiomate.cmd");
            HintText.Text = Strings.Get("Finish_HintWithWorkspace");
            LaunchBtn.IsEnabled = File.Exists(_launcherCmd);
            LaunchBtn.Visibility = Visibility.Visible;
        }
        else
        {
            HintText.Text = Strings.Get("Finish_HintNoWorkspace");
            LaunchBtn.IsEnabled = false;
            LaunchBtn.Visibility = Visibility.Collapsed;
        }
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_launcherCmd) { UseShellExecute = true });
            ((MainWindow)Window.GetWindow(this)!).Close();
        }
        catch { }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        ((MainWindow)Window.GetWindow(this)!).Close();
    }
}
