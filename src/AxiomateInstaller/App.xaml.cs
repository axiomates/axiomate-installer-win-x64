using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AxiomateInstaller.Services;

namespace AxiomateInstaller;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Pick the initial UI language from the OS culture (zh-* → zh, else → en).
        // The Welcome page lets the user override this once before continuing.
        Strings.Apply(Strings.DetectFromCulture());
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowError(Strings.Get("Dlg_Err_DispatcherTitle"), e.Exception);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) ShowError(Strings.Get("Dlg_Err_DomainTitle"), ex);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ShowError(Strings.Get("Dlg_Err_TaskTitle"), e.Exception);
        e.SetObserved();
    }

    public static void ShowError(string title, Exception ex)
    {
        try
        {
            string body = Strings.Format("Dlg_Err_Body_Format", ex.GetType().Name, ex.Message);
            string caption = $"{Strings.Get("Dlg_Err_TitleSuffix")} - {title}";
            MessageBox.Show(body, caption, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { /* swallow secondary failures */ }
    }
}
