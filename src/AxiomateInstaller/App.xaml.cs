using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace AxiomateInstaller;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowError("界面线程异常", e.Exception);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) ShowError("应用域异常", ex);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ShowError("后台任务异常", e.Exception);
        e.SetObserved();
    }

    public static void ShowError(string title, Exception ex)
    {
        try
        {
            string body = $"{ex.GetType().Name}: {ex.Message}\n\n详细信息已写入日志，可在弹窗中查看。";
            MessageBox.Show(body, $"Axiomate 安装器 - {title}", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { /* swallow secondary failures */ }
    }
}
