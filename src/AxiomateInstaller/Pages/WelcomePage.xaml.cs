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
        // Once the user advances past Welcome, the radio buttons are locked.
        LangZh.IsEnabled = LangEn.IsEnabled = !host.LanguageLocked;
        _suppress = false;

        SubtitleText.Text = Strings.Format("Welcome_Subtitle_Format",
            host.Version.InstallerVersion, host.Version.AxiomateVersion);
    }

    private void LangZh_Checked(object sender, System.Windows.RoutedEventArgs e) => Switch(UiLang.Zh);
    private void LangEn_Checked(object sender, System.Windows.RoutedEventArgs e) => Switch(UiLang.En);

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
