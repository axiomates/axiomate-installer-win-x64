using System;
using System.Diagnostics;
using System.Security.Principal;
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

        if (!IsElevated())
        {
            RelaunchElevatedWithTargetUser();
            Shutdown();
            return;
        }

        TargetUserContext.Initialize(GetArgValue(e.Args, "--target-user-sid"), GetArgValue(e.Args, "--target-user-profile"));
        if (TargetUserContext.Sid is null || TargetUserContext.Profile is null)
        {
            TargetUserContext.Initialize(WindowsIdentity.GetCurrent().User?.Value,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        base.OnStartup(e);
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void RelaunchElevatedWithTargetUser()
    {
        string exe = Process.GetCurrentProcess().MainModule?.FileName ?? Environment.ProcessPath ?? "";
        string sid = WindowsIdentity.GetCurrent().User?.Value ?? "";
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            Verb = "runas",
        };
        psi.ArgumentList.Add("--elevated");
        psi.ArgumentList.Add("--target-user-sid");
        psi.ArgumentList.Add(sid);
        psi.ArgumentList.Add("--target-user-profile");
        psi.ArgumentList.Add(profile);
        Process.Start(psi);
    }

    private static string? GetArgValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        }
        return null;
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
