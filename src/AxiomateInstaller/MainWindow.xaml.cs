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

    private readonly List<WizardPage> _pages = new();
    private int _index;

    public MainWindow()
    {
        Version = VersionInfo.Load();
        Log = new Logger(Version.InstallerVersion);
        InitializeComponent();
        Title = $"Axiomate Installer {Version.InstallerVersion}";
        VersionBadge.Text = $"v{Version.InstallerVersion}  ·  Axiomate {Version.AxiomateVersion}";
        Closed += (_, _) => Log.Dispose();

        // Build wizard pages.
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
        // The page validates first.
        var page = _pages[_index];
        if (!page.Validate(this)) return;
        page.OnLeave(this);

        int next = page.NextIndex(this, _index);
        if (next == _index) return; // page handles its own transition (e.g. progress runs)
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
        NextBtn.Content     = page.NextLabel;
        CancelBtn.IsEnabled = page.AllowCancel;
        HeaderSubtitle.Text = page.HeaderSubtitle;
    }

    private void NextBtn_Click(object sender, RoutedEventArgs e)   => GoNext();
    private void BackBtn_Click(object sender, RoutedEventArgs e)   => GoBack();
    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("确定要退出安装器吗？", "退出 Axiomate Installer",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
        {
            Close();
        }
    }

    private void AboutLink_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        string body =
            $"Axiomate Installer\n" +
            $"安装器版本: {Version.InstallerVersion}\n" +
            $"Axiomate:    {Version.AxiomateVersion}\n" +
            $"内置 Git:    {Version.BundledGitVersion}\n" +
            $"内置 Python: {Version.BundledPythonVersion}\n\n" +
            $"官网: https://axiomate.net";
        MessageBox.Show(body, "关于", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
