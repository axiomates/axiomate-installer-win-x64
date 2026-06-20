using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AxiomateInstaller.Services;

/// <summary>
/// A small modal confirm dialog whose title, body and buttons are all sourced
/// from the active language dictionary, so the buttons follow the installer's
/// chosen language rather than the OS (which is what WPF MessageBox does).
/// </summary>
public static class LocalizedDialog
{
    /// <summary>Shows a two-button confirm dialog. Returns true if confirmed.</summary>
    public static bool Confirm(string title, string body, string confirmKey, string cancelKey)
    {
        var win = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.Height,
            Width = 460,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.SingleBorderWindow,
            ShowInTaskbar = false,
            Background = (Brush)Application.Current.Resources["Bg"],
            FontFamily = new FontFamily("Segoe UI, Microsoft YaHei, PingFang SC"),
            FontSize = 14,
        };
        if (Application.Current.MainWindow is { IsLoaded: true } owner && owner != win)
            win.Owner = owner;

        bool result = false;

        var root = new StackPanel { Margin = new Thickness(22) };

        root.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["Text"],
            Margin = new Thickness(0, 0, 0, 10),
        });

        root.Children.Add(new TextBlock
        {
            Text = body,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Foreground = (Brush)Application.Current.Resources["Text"],
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0),
        };

        var confirmBtn = new Button
        {
            Content = Strings.Get(confirmKey),
            Style = (Style)Application.Current.Resources["PrimaryButton"],
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true,
        };
        confirmBtn.Click += (_, _) => { result = true; win.Close(); };

        var cancelBtn = new Button
        {
            Content = Strings.Get(cancelKey),
            Style = (Style)Application.Current.Resources["SecondaryButton"],
            IsCancel = true,
        };
        cancelBtn.Click += (_, _) => { result = false; win.Close(); };

        buttons.Children.Add(confirmBtn);
        buttons.Children.Add(cancelBtn);
        root.Children.Add(buttons);

        win.Content = root;
        win.ShowDialog();
        return result;
    }
}
