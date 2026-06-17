using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AxiomateInstaller.Services;

namespace AxiomateInstaller.Pages;

public partial class ProgressPage : WizardPage
{
    public override string HeaderSubtitle => "正在安装";
    public override bool AllowBack   => false;
    public override bool AllowNext   => false;
    public override bool AllowCancel => false;

    private bool _started;

    public ProgressPage() { InitializeComponent(); }

    public override async void OnEnter(MainWindow host)
    {
        if (_started) return;
        _started = true;

        host.Log.OnLine += AppendLog;

        var progress = new Progress<InstallProgress>(p =>
        {
            StepText.Text = $"步骤 {p.CurrentStep}/{p.TotalSteps} : {p.Title}";
            Bar.Value = p.TotalSteps == 0 ? 0 : (100.0 * p.CurrentStep / p.TotalSteps);
        });

        try
        {
            var engine = new InstallEngine(host.Log, host.Version);
            await engine.RunAsync(host.Options, progress);
            Bar.Value = 100;
            StepText.Text = "安装完成！";
            host.Log.Info("Installation finished successfully.");
            // Move to finish page.
            host.ShowPage(GetFinishIndex(host));
        }
        catch (InstallStepException ex)
        {
            host.Log.Error("Install step failed", ex);
            ShowFailure(host, ex.Message);
        }
        catch (Exception ex)
        {
            host.Log.Error("Unexpected install failure", ex);
            ShowFailure(host, $"未预期的错误: {ex.Message}");
        }
    }

    private void AppendLog(string line)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            LogText.Text += line + Environment.NewLine;
            LogScroll.ScrollToEnd();
        }));
    }

    private void ShowFailure(MainWindow host, string message)
    {
        StepText.Text = "安装失败";
        StepText.Foreground = System.Windows.Media.Brushes.Crimson;
        Bar.Foreground = System.Windows.Media.Brushes.Crimson;

        string log = host.Log.LogFilePath;
        var res = MessageBox.Show(
            $"{message}\n\n详细日志: {log}\n\n点击「是」打开日志文件。",
            "Axiomate 安装失败",
            MessageBoxButton.YesNo,
            MessageBoxImage.Error);
        if (res == MessageBoxResult.Yes)
        {
            try { Process.Start(new ProcessStartInfo("notepad.exe", $"\"{log}\"") { UseShellExecute = true }); }
            catch { }
        }
        // Allow user to close the window after failure.
        host.CancelBtn.IsEnabled = true;
    }

    private static int GetFinishIndex(MainWindow host) => 6; // index of FinishPage in the array
}
