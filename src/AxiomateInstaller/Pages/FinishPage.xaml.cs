using System;
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


    public FinishPage() { InitializeComponent(); }

    public override void OnEnter(MainWindow host)
    {
        DetailText.Text = Strings.Format("Finish_Detail_Format", host.Options.InstallDir);

        if (host.Options.CreateWorkspace)
        {
            HintText.Text = Strings.Get("Finish_HintWithWorkspace");
            string desktopShortcut = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                "Axiomate.lnk");
            string startMenuShortcut = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                "Programs",
                "Axiomate.lnk");
            LaunchBtn.IsEnabled = File.Exists(desktopShortcut) || File.Exists(startMenuShortcut);
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
            AxiomateLauncher.LaunchUnelevatedFromShortcut();
            ((MainWindow)Window.GetWindow(this)!).Close();
        }
        catch { }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        ((MainWindow)Window.GetWindow(this)!).Close();
    }
}
