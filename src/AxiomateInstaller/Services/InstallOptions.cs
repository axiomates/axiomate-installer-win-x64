using System;

namespace AxiomateInstaller.Services;

/// <summary>
/// All user choices collected by the wizard. Single mutable bag passed page-to-page.
/// </summary>
public sealed class InstallOptions
{
    public string InstallDir { get; set; } = @"%USERPROFILE%\axiomate";

    // Env-check page outcomes & user choices
    public bool NeedsGitInstall    { get; set; }
    public bool NeedsPythonInstall { get; set; }
    public bool ConfigurePipMirror { get; set; } = true;
    public PipMirror PipMirrorChoice { get; set; } = PipMirror.Tsinghua;

    // Options page choices
    public bool QuickModelConfig { get; set; }
    public bool ClearConfigDir { get; set; }
    public bool EnableBypassPermissions { get; set; }
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

public enum ModelSite
{
    Deepseek,
    Zhipu
}

public enum ModelChoice
{
    DeepseekV4Pro,
    DeepseekV4Flash,
    Glm52,
    Glm47
}

public static class ModelSiteExtensions
{
    /// <summary>Models offered for this site, in display order. First is the default.</summary>
    public static ModelChoice[] Models(this ModelSite s) => s switch
    {
        ModelSite.Deepseek => new[] { ModelChoice.DeepseekV4Pro, ModelChoice.DeepseekV4Flash },
        ModelSite.Zhipu    => new[] { ModelChoice.Glm52, ModelChoice.Glm47 },
        _ => throw new ArgumentOutOfRangeException(nameof(s))
    };

    /// <summary>Resource key for the site label shown in the provider dropdown.</summary>
    public static string LabelKey(this ModelSite s) => s switch
    {
        ModelSite.Deepseek => "Model_SiteDeepseek",
        ModelSite.Zhipu    => "Model_SiteZhipu",
        _ => throw new ArgumentOutOfRangeException(nameof(s))
    };

    /// <summary>Console page where the user creates an API key for this site.</summary>
    public static string ApiKeyUrl(this ModelSite s) => s switch
    {
        ModelSite.Deepseek => "https://platform.deepseek.com/api_keys",
        ModelSite.Zhipu    => "https://bigmodel.cn/usercenter/apikeys",
        _ => throw new ArgumentOutOfRangeException(nameof(s))
    };

    /// <summary>Resource key for the "get your key from …" link text.</summary>
    public static string FooterLinkKey(this ModelSite s) => s switch
    {
        ModelSite.Deepseek => "Model_Footer_Link_Deepseek",
        ModelSite.Zhipu    => "Model_Footer_Link_Zhipu",
        _ => throw new ArgumentOutOfRangeException(nameof(s))
    };
}

public static class ModelChoiceExtensions
{
    public static string TemplateFile(this ModelChoice c) => c switch
    {
        ModelChoice.DeepseekV4Pro   => "deepseek-v4-pro.json",
        ModelChoice.DeepseekV4Flash => "deepseek-v4-flash.json",
        ModelChoice.Glm52           => "glm-5.2.json",
        ModelChoice.Glm47           => "glm-4.7.json",
        _ => throw new ArgumentOutOfRangeException(nameof(c))
    };

    public static string DisplayName(this ModelChoice c) => c switch
    {
        ModelChoice.DeepseekV4Pro   => "DeepSeek V4 Pro",
        ModelChoice.DeepseekV4Flash => "DeepSeek V4 Flash",
        ModelChoice.Glm52           => "GLM-5.2",
        ModelChoice.Glm47           => "GLM-4.7",
        _ => throw new ArgumentOutOfRangeException(nameof(c))
    };

    /// <summary>Resource key for the per-model line shown in the model dropdown.</summary>
    public static string ItemKey(this ModelChoice c) => c switch
    {
        ModelChoice.DeepseekV4Pro   => "Model_Item_Pro",
        ModelChoice.DeepseekV4Flash => "Model_Item_Flash",
        ModelChoice.Glm52           => "Model_Item_Glm52",
        ModelChoice.Glm47           => "Model_Item_Glm47",
        _ => throw new ArgumentOutOfRangeException(nameof(c))
    };

    /// <summary>The site this model belongs to (used to restore site selection on re-entry).</summary>
    public static ModelSite Site(this ModelChoice c) => c switch
    {
        ModelChoice.DeepseekV4Pro or ModelChoice.DeepseekV4Flash => ModelSite.Deepseek,
        ModelChoice.Glm52 or ModelChoice.Glm47 => ModelSite.Zhipu,
        _ => throw new ArgumentOutOfRangeException(nameof(c))
    };
}
