using System.Windows;
using System.Windows.Controls;
using AxiomateInstaller.Services;

namespace AxiomateInstaller.Pages;

public partial class ModelConfigPage : WizardPage
{
    public override string HeaderSubtitleKey => "Model_PageSubtitle";

    public ModelConfigPage() { InitializeComponent(); }

    public override void OnEnter(MainWindow host)
    {
        ModelCombo.SelectedIndex = host.Options.ModelChoice == ModelChoice.DeepseekV4Pro ? 0 : 1;
        KeyBox.Password  = host.Options.ApiKey;
        KeyBoxPlain.Text = host.Options.ApiKey;
    }

    private void ShowKey_Changed(object sender, RoutedEventArgs e)
    {
        if (ShowKeyChk.IsChecked == true)
        {
            KeyBoxPlain.Text = KeyBox.Password;
            KeyBoxPlain.Visibility = Visibility.Visible;
            KeyBox.Visibility      = Visibility.Collapsed;
        }
        else
        {
            KeyBox.Password = KeyBoxPlain.Text;
            KeyBoxPlain.Visibility = Visibility.Collapsed;
            KeyBox.Visibility      = Visibility.Visible;
        }
    }

    private void KeyBoxPlain_Changed(object sender, TextChangedEventArgs e) { /* mirror handled at toggle time */ }

    public override bool Validate(MainWindow host)
    {
        string key = ShowKeyChk.IsChecked == true ? KeyBoxPlain.Text : KeyBox.Password;
        if (string.IsNullOrWhiteSpace(key))
        {
            MessageBox.Show(Strings.Get("Model_BadKeyBody"), Strings.Get("Model_BadKeyTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    public override void OnLeave(MainWindow host)
    {
        if (ModelCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            host.Options.ModelChoice = tag == "DeepseekV4Flash"
                ? ModelChoice.DeepseekV4Flash
                : ModelChoice.DeepseekV4Pro;
        }
        host.Options.ApiKey = (ShowKeyChk.IsChecked == true ? KeyBoxPlain.Text : KeyBox.Password).Trim();
    }
}
