using System;
using System.IO;
using AxiomateInstaller.Services;

namespace AxiomateInstaller.Pages;

public partial class FinishPage : WizardPage
{
    public override string HeaderSubtitleKey => "Finish_PageSubtitle";
    public override bool AllowBack   => false;
    public override bool AllowCancel => false;
    public override string NextLabelKey => _canLaunch ? "Finish_LaunchBtn" : "Btn_Finish";

    private bool _canLaunch;

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
            _canLaunch = File.Exists(desktopShortcut) || File.Exists(startMenuShortcut);
        }
        else
        {
            HintText.Text = Strings.Get("Finish_HintNoWorkspace");
            _canLaunch = false;
        }
    }

    public override void OnLeave(MainWindow host)
    {
        if (_canLaunch)
        {
            try { AxiomateLauncher.LaunchUnelevatedFromShortcut(); }
            catch (Exception ex) { host.Log.Warn($"Could not launch Axiomate from shortcut: {ex.Message}"); }
        }
        host.Close();
    }

    public override int NextIndex(MainWindow host, int current) => current;
}
