using AxiomateInstaller.Services;

namespace AxiomateInstaller.Pages;

public partial class WelcomePage : WizardPage
{
    public override string HeaderSubtitleKey => "Welcome_PageSubtitle";
    public override bool AllowBack => false;

    private bool _suppress;

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
        var prior = PriorInstallDetector.Detect();
        if (prior is null)
        {
            host.Options.PriorInstallDir = null;
            return true;
        }

        bool ok = LocalizedDialog.Confirm(
            Strings.Get("Path_PriorInstallTitle"),
            Strings.Format("Path_PriorInstallBody_Format", prior.InstallDir),
            "Btn_Continue",
            "Btn_Cancel");
        if (!ok) return false;

        host.Options.PriorInstallDir = prior.InstallDir;
        return true;
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
