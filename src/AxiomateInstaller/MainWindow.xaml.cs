using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using AxiomateInstaller.Pages;
using AxiomateInstaller.Services;

namespace AxiomateInstaller;

public partial class MainWindow : System.Windows.Window
{
    public VersionInfo Version { get; }
    public Logger Log { get; }
    public InstallOptions Options { get; } = new();
    public EnvCheckResult? LastEnvCheck { get; set; }
    public bool LanguageLocked { get; set; }

    private readonly List<WizardPage> _pages = new();
    private int _index;

    public MainWindow()
    {
        Version = VersionInfo.Load();
        Log = new Logger(Version.InstallerVersion);
        Log.Info($"UI language at startup: {Strings.Current}");
        InitializeComponent();
        VersionBadge.Text = $"v{Version.InstallerVersion}  ·  Axiomate {Version.AxiomateVersion}";
        Closed += (_, _) => Log.Dispose();

        _pages.Add(new WelcomePage());
        _pages.Add(new EnvCheckPage());
        _pages.Add(new InstallPathPage());
        _pages.Add(new OptionsPage());
        _pages.Add(new ModelConfigPage());
        _pages.Add(new ProgressPage());
        _pages.Add(new FinishPage());

        ShowPage(0);
    }

    public void ShowPage(int idx)
    {
        if (idx < 0 || idx >= _pages.Count) return;
        _index = idx;
        var page = _pages[idx];
        ContentFrame.Navigate(page);
        page.OnEnter(this);
        UpdateChrome(page);
    }

    public void GoNext()
    {
        var page = _pages[_index];
        if (!page.Validate(this)) return;
        page.OnLeave(this);

        // The Welcome page is the only place language can change. Once we leave it,
        // the language is locked for the rest of the wizard so dialogs/log don't drift.
        if (page is WelcomePage) LanguageLocked = true;

        int next = page.NextIndex(this, _index);
        if (next == _index) return;
        ShowPage(next);
    }

    public void GoBack()
    {
        var page = _pages[_index];
        int prev = page.PrevIndex(this, _index);
        if (prev < 0) return;
        ShowPage(prev);
    }

    public void UpdateChrome(WizardPage page)
    {
        BackBtn.IsEnabled   = page.AllowBack;
        NextBtn.IsEnabled   = page.AllowNext;
        NextBtn.Content     = Strings.Get(page.NextLabelKey);
        CancelBtn.IsEnabled = page.AllowCancel;
        HeaderSubtitle.Text = Strings.Get(page.HeaderSubtitleKey);
    }

    private void NextBtn_Click(object sender, RoutedEventArgs e)   => GoNext();
    private void BackBtn_Click(object sender, RoutedEventArgs e)   => GoBack();
    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Strings.Get("Confirm_ExitBody"), Strings.Get("Confirm_ExitTitle"),
                MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
        {
            Close();
        }
    }

    private void AboutLink_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        string body = Strings.Format("About_Body_Format",
            Version.InstallerVersion, Version.AxiomateVersion,
            Version.BundledGitVersion, Version.BundledPythonVersion);
        MessageBox.Show(body, Strings.Get("About_Title"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void WebsiteLink_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("http://axiomate.net") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not open website: {ex.Message}");
        }
    }

    /// <summary>Welcome page calls this when the user picks a language.</summary>
    public void RefreshAfterLanguageChange()
    {
        // Re-apply chrome strings so already-bound DynamicResource refresh and explicit
        // text values pull from the new dictionary.
        UpdateChrome(_pages[_index]);
        // Re-enter the current page so its dynamic-content code-behind also refreshes.
        _pages[_index].OnEnter(this);
    }
}
