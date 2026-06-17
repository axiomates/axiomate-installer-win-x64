using System;

namespace AxiomateInstaller.Services;

/// <summary>
/// All user choices collected by the wizard. Single mutable bag passed page-to-page.
/// </summary>
public sealed class InstallOptions
{
    public string InstallDir { get; set; } = @"C:\Program Files\Axiomate";

    // Env-check page outcomes & user choices
    public bool NeedsGitInstall    { get; set; }
    public bool NeedsPythonInstall { get; set; }
    public bool ConfigurePipMirror { get; set; } = true;
    public PipMirror PipMirrorChoice { get; set; } = PipMirror.Tsinghua;

    // Options page choices
    public bool QuickModelConfig { get; set; }
    public bool CreateWorkspace  { get; set; }
    public string WorkspaceDir   { get; set; } = @"%USERPROFILE%\axiomate-workspace";

    // Model-config page choices
    public ModelChoice ModelChoice { get; set; } = ModelChoice.DeepseekV4Pro;
    public string ApiKey { get; set; } = "";

    // Resolved at run time
    public string PythonInstallDir { get; set; } = @"C:\Program Files\Python312";
}

public enum PipMirror
{
    Tsinghua,
    Aliyun
}

public static class PipMirrorExtensions
{
    public static string IndexUrl(this PipMirror m) => m switch
    {
        PipMirror.Tsinghua => "https://pypi.tuna.tsinghua.edu.cn/simple",
        PipMirror.Aliyun   => "https://mirrors.aliyun.com/pypi/simple/",
        _ => throw new ArgumentOutOfRangeException(nameof(m))
    };

    public static string TrustedHost(this PipMirror m) => m switch
    {
        PipMirror.Tsinghua => "pypi.tuna.tsinghua.edu.cn",
        PipMirror.Aliyun   => "mirrors.aliyun.com",
        _ => throw new ArgumentOutOfRangeException(nameof(m))
    };

    public static string DisplayName(this PipMirror m) => m switch
    {
        PipMirror.Tsinghua => "清华大学 TUNA",
        PipMirror.Aliyun   => "阿里云",
        _ => throw new ArgumentOutOfRangeException(nameof(m))
    };
}

public enum ModelChoice
{
    DeepseekV4Pro,
    DeepseekV4Flash
}

public static class ModelChoiceExtensions
{
    public static string TemplateFile(this ModelChoice c) => c switch
    {
        ModelChoice.DeepseekV4Pro   => "deepseek-v4-pro.json",
        ModelChoice.DeepseekV4Flash => "deepseek-v4-flash.json",
        _ => throw new ArgumentOutOfRangeException(nameof(c))
    };

    public static string DisplayName(this ModelChoice c) => c switch
    {
        ModelChoice.DeepseekV4Pro   => "DeepSeek V4 Pro",
        ModelChoice.DeepseekV4Flash => "DeepSeek V4 Flash",
        _ => throw new ArgumentOutOfRangeException(nameof(c))
    };
}
