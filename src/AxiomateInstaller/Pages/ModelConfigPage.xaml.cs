using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using AxiomateInstaller.Services;

namespace AxiomateInstaller.Pages;

public partial class ModelConfigPage : WizardPage
{
    public override string HeaderSubtitleKey => "Model_PageSubtitle";
    public override string NextLabelKey => "Btn_StartInstall";

    // Guards SiteCombo_SelectionChanged while OnEnter sets the initial selection,
    // so the model list isn't rebuilt mid-initialization.
    private bool _initializing;

    public ModelConfigPage() { InitializeComponent(); }

    public override void OnEnter(MainWindow host)
    {
        _initializing = true;

        // Populate the provider dropdown from the ModelSite enum.
        SiteCombo.Items.Clear();
        foreach (ModelSite site in Enum.GetValues<ModelSite>())
        {
            SiteCombo.Items.Add(new ComboBoxItem
            {
                Content = Strings.Get(site.LabelKey()),
                Tag = site,
            });
        }

        ModelSite selectedSite = host.Options.ModelChoice.Site();
        SiteCombo.SelectedIndex = Array.IndexOf(Enum.GetValues<ModelSite>(), selectedSite);

        PopulateModels(selectedSite, host.Options.ModelChoice);

        KeyBox.Password  = host.Options.ApiKey;
        KeyBoxPlain.Text = host.Options.ApiKey;

        _initializing = false;
    }

    /// <summary>Fill the model dropdown with the given site's models, selecting `preferred` if it belongs to the site (else the first).</summary>
    private void PopulateModels(ModelSite site, ModelChoice preferred)
    {
        ModelCombo.Items.Clear();
        int selectedIndex = 0;
        ModelChoice[] models = site.Models();
        for (int i = 0; i < models.Length; i++)
        {
            ModelChoice model = models[i];
            ModelCombo.Items.Add(new ComboBoxItem
            {
                Content = Strings.Get(model.ItemKey()),
                Tag = model,
            });
            if (model == preferred) selectedIndex = i;
        }
        ModelCombo.SelectedIndex = selectedIndex;

        UpdateApiKeyLink(site);
    }

    /// <summary>Point the footer link text at the selected site's console.</summary>
    private void UpdateApiKeyLink(ModelSite site)
    {
        ApiKeyLinkText.Text = Strings.Get(site.FooterLinkKey());
    }

    private void SiteCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (SiteCombo.SelectedItem is ComboBoxItem item && item.Tag is ModelSite site)
        {
            // Default to the site's first model on an explicit site switch.
            PopulateModels(site, site.Models()[0]);
        }
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

    private void ApiKeyPlatformLink_Click(object sender, RoutedEventArgs e)
    {
        ModelSite site = SiteCombo.SelectedItem is ComboBoxItem item && item.Tag is ModelSite s
            ? s
            : ModelSite.Deepseek;
        try
        {
            Process.Start(new ProcessStartInfo(site.ApiKeyUrl()) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Non-fatal: user can still continue after reading the link text.
        }
    }

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
        if (ModelCombo.SelectedItem is ComboBoxItem item && item.Tag is ModelChoice model)
        {
            host.Options.ModelChoice = model;
        }
        host.Options.ApiKey = (ShowKeyChk.IsChecked == true ? KeyBoxPlain.Text : KeyBox.Password).Trim();
    }
}
