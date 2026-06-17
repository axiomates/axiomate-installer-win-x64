using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using AxiomateInstaller.Services;

namespace AxiomateInstaller.Pages;

public partial class ProgressPage : WizardPage
{
    public override string HeaderSubtitleKey => "Progress_PageSubtitle";
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
            StepText.Text = Strings.Format("Progress_Step_Format", p.CurrentStep, p.TotalSteps, p.LocalizedTitle());
            Bar.Value = p.TotalSteps == 0 ? 0 : (100.0 * p.CurrentStep / p.TotalSteps);
        });

        try
        {
            var engine = new InstallEngine(host.Log, host.Version);
            await engine.RunAsync(host.Options, progress);
            Bar.Value = 100;
            StepText.Text = Strings.Get("Progress_Done");
            host.Log.Info("Installation finished successfully.");
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
            ShowFailure(host, Strings.Format("Progress_UnexpectedFailure_Format", ex.Message));
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
        StepText.Text = Strings.Get("Progress_Failed");
        StepText.Foreground = System.Windows.Media.Brushes.Crimson;
        Bar.Foreground = System.Windows.Media.Brushes.Crimson;

        string log = host.Log.LogFilePath;
        var res = MessageBox.Show(
            Strings.Format("Progress_FailDialogBody_Format", message, log),
            Strings.Get("Progress_FailDialogTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Error);
        if (res == MessageBoxResult.Yes)
        {
            try
            {
                var psi = new ProcessStartInfo("notepad.exe") { UseShellExecute = true };
                psi.ArgumentList.Add(log);
                Process.Start(psi);
            }
            catch { }
        }
        host.CancelBtn.IsEnabled = true;
    }

    private static int GetFinishIndex(MainWindow host) => 6;
}
