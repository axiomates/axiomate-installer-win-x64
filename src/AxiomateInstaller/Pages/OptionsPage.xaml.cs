using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AxiomateInstaller.Services;

namespace AxiomateInstaller.Pages;

public partial class OptionsPage : WizardPage
{
    private bool _dragScrolling;
    private bool _dragMoved;
    private Point _dragStart;
    private double _dragStartOffset;

    public override string HeaderSubtitleKey => "Opt_PageSubtitle";

    public OptionsPage() { InitializeComponent(); }

    public override void OnEnter(MainWindow host)
    {
        ModelChk.IsChecked      = host.Options.QuickModelConfig;
        BypassPermChk.IsChecked = host.Options.EnableBypassPermissions;
        WorkspaceChk.IsChecked  = host.Options.CreateWorkspace;
        WorkspacePathBox.Text  = host.Options.WorkspaceDir;
        UpdateWorkspaceEnabled();
    }

    private void WorkspaceChk_Toggle(object sender, RoutedEventArgs e) => UpdateWorkspaceEnabled();

    private void UpdateWorkspaceEnabled()
    {
        if (WorkspacePathGrid != null)
            WorkspacePathGrid.IsEnabled = WorkspaceChk.IsChecked == true;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        string userProfile = UserProfileResolver.GetUserProfile();
        string resolvedWorkspace = WorkspacePathGuard.ResolveForUser(WorkspacePathBox.Text, userProfile);
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Strings.Get("Opt_PickerTitle"),
            InitialDirectory = Directory.Exists(resolvedWorkspace)
                ? resolvedWorkspace
                : userProfile
        };
        if (dlg.ShowDialog() == true) WorkspacePathBox.Text = dlg.FolderName;
    }

    private void OptionsScroll_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInteractiveElement(e.OriginalSource as DependencyObject)) return;
        _dragScrolling = true;
        _dragMoved = false;
        _dragStart = e.GetPosition(OptionsScroll);
        _dragStartOffset = OptionsScroll.VerticalOffset;
        OptionsScroll.CaptureMouse();
        e.Handled = true;
    }

    private void OptionsScroll_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragScrolling || e.LeftButton != MouseButtonState.Pressed) return;
        Point current = e.GetPosition(OptionsScroll);
        double delta = _dragStart.Y - current.Y;
        if (!_dragMoved && Math.Abs(delta) < 2) return;
        _dragMoved = true;
        OptionsScroll.ScrollToVerticalOffset(_dragStartOffset + delta);
        e.Handled = true;
    }

    private void OptionsScroll_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        bool handled = _dragMoved;
        EndDragScroll(releaseCapture: true);
        e.Handled = handled;
    }

    private void OptionsScroll_LostMouseCapture(object sender, MouseEventArgs e) => EndDragScroll(releaseCapture: false);

    private void EndDragScroll(bool releaseCapture)
    {
        if (!_dragScrolling) return;
        _dragScrolling = false;
        _dragMoved = false;
        if (releaseCapture && OptionsScroll.IsMouseCaptured)
            OptionsScroll.ReleaseMouseCapture();
    }

    private static bool IsInteractiveElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase or TextBoxBase or Selector) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    public override bool Validate(MainWindow host)
    {
        if (WorkspaceChk.IsChecked != true) return true;
        string p = WorkspacePathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(p))
        {
            MessageBox.Show(Strings.Get("Opt_BadWorkspaceBody_Empty"), Strings.Get("Opt_BadWorkspaceTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        try
        {
            string userProfile = UserProfileResolver.GetUserProfile(host.Log);
            _ = WorkspacePathGuard.ResolveForUser(p, userProfile);
            WorkspacePathGuard.EnsureSeparateFromInstallDir(p, host.Options.InstallDir, userProfile);
        }
        catch (InstallStepException ex)
        {
            MessageBox.Show(ex.Message, Strings.Get("Opt_BadWorkspaceTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        catch
        {
            MessageBox.Show(Strings.Get("Opt_BadWorkspaceBody_Invalid"), Strings.Get("Opt_BadWorkspaceTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    public override void OnLeave(MainWindow host)
    {
        host.Options.QuickModelConfig = ModelChk.IsChecked == true;
        host.Options.EnableBypassPermissions = BypassPermChk.IsChecked == true;
        host.Options.CreateWorkspace  = WorkspaceChk.IsChecked == true;
        host.Options.WorkspaceDir     = WorkspacePathBox.Text.Trim().TrimEnd('\\');
    }

    public override int NextIndex(MainWindow host, int current)
    {
        return host.Options.QuickModelConfig ? current + 1 : current + 2;
    }
}
