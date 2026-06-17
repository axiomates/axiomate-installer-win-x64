using System.IO;
using System.Windows;
using AxiomateInstaller.Services;

namespace AxiomateInstaller.Pages;

public partial class InstallPathPage : WizardPage
{
    public override string HeaderSubtitleKey => "Path_PageSubtitle";

    public InstallPathPage() { InitializeComponent(); }

    public override void OnEnter(MainWindow host)
    {
        PathBox.Text = host.Options.InstallDir;
        UpdateHint();
        PathBox.TextChanged += (_, _) => UpdateHint();
    }

    private void UpdateHint()
    {
        var eval = DirGuard.Evaluate(PathBox.Text);
        if (!eval.Ok)
        {
            WarnText.Text = eval.Reason ?? "";
            HintText.Text = "";
            return;
        }
        WarnText.Text = "";
        HintText.Text = DirGuard.DirectoryHasContent(PathBox.Text)
            ? Strings.Get("Path_Hint_NonEmpty")
            : Strings.Get("Path_Hint_Empty");
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Strings.Get("Path_PickerTitle"),
            InitialDirectory = Directory.Exists(PathBox.Text) ? PathBox.Text : @"C:\Program Files"
        };
        if (dlg.ShowDialog() == true) PathBox.Text = dlg.FolderName;
    }

    public override bool Validate(MainWindow host)
    {
        var eval = DirGuard.Evaluate(PathBox.Text);
        if (!eval.Ok)
        {
            MessageBox.Show(eval.Reason, Strings.Get("Path_BadTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (DirGuard.DirectoryHasContent(PathBox.Text))
        {
            var res = MessageBox.Show(
                Strings.Format("Path_ConfirmWipeBody_Format", PathBox.Text),
                Strings.Get("Path_ConfirmWipeTitle"),
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);
            if (res != MessageBoxResult.OK) return false;
        }
        return true;
    }

    public override void OnLeave(MainWindow host)
    {
        host.Options.InstallDir = PathBox.Text.Trim().TrimEnd('\\');
    }
}
