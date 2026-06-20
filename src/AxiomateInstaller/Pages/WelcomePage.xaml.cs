using System;
using System.Threading.Tasks;
using AxiomateInstaller.Services;

namespace AxiomateInstaller.Pages;

public partial class WelcomePage : WizardPage
{
    public override string HeaderSubtitleKey => "Welcome_PageSubtitle";
    public override bool AllowBack => false;

    private bool _suppress;
    private bool _uninstalling;

    public WelcomePage() { InitializeComponent(); }

    public override void OnEnter(MainWindow host)
    {
        _suppress = true;
        if (Strings.Current == UiLang.Zh) LangZh.IsChecked = true;
        else                              LangEn.IsChecked = true;
        // Language switching is allowed whenever the user is on Welcome.
        LangZh.IsEnabled = LangEn.IsEnabled = true;
        _suppress = false;

        SubtitleText.Text = Strings.Format("Welcome_Subtitle_Format",
            host.Version.InstallerVersion, host.Version.AxiomateVersion);
    }

    private void LangZh_Checked(object sender, System.Windows.RoutedEventArgs e) => Switch(UiLang.Zh);
    private void LangEn_Checked(object sender, System.Windows.RoutedEventArgs e) => Switch(UiLang.En);

    public override bool Validate(MainWindow host)
    {
        // If a previous run already removed it, proceed.
        var prior = PriorInstallDetector.Detect();
        if (prior is null) return true;

        bool ok = LocalizedDialog.Confirm(
            Strings.Get("Path_PriorInstallTitle"),
            Strings.Format("Path_PriorInstallBody_Format", prior.InstallDir),
            "Btn_Continue",
            "Btn_Cancel");
        if (!ok) return false;

        // Uninstall now (the wizard is already elevated), then advance once done.
        _ = UninstallThenAdvanceAsync(host, prior.InstallDir);
        return false; // navigation is driven by the async flow below
    }

    private async Task UninstallThenAdvanceAsync(MainWindow host, string priorDir)
    {
        if (_uninstalling) return;
        _uninstalling = true;
        SetBusy(host, true);
        try
        {
            await Task.Run(() => new PriorUninstaller(host.Log).Run(priorDir));
        }
        catch (Exception ex)
        {
            host.Log.Warn($"Prior uninstall failed: {ex.Message}");
        }
        finally
        {
            _uninstalling = false;
            SetBusy(host, false);
        }

        host.GoNext();
    }

    private void SetBusy(MainWindow host, bool busy)
    {
        BusyOverlay.Visibility = busy ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        host.NextBtn.IsEnabled = !busy;
        host.CancelBtn.IsEnabled = !busy;
        LangZh.IsEnabled = LangEn.IsEnabled = !busy;
    }

    private void Switch(UiLang lang)
    {
        if (_suppress) return;
        if (Strings.Current == lang) return;
        Strings.Apply(lang);
        var host = (MainWindow)System.Windows.Window.GetWindow(this)!;
        host.Log.Info($"User picked UI language: {lang}");
        host.RefreshAfterLanguageChange();
        // Refresh the subtitle (string.Format result, not bound).
        SubtitleText.Text = Strings.Format("Welcome_Subtitle_Format",
            host.Version.InstallerVersion, host.Version.AxiomateVersion);
    }
}
