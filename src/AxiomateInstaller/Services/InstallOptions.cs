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
    public bool ClearConfigJson { get; set; }
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
    Zhipu,
    ZhipuIntl,
    Moonshot,
    MoonshotIntl
}

public enum ModelChoice
{
    DeepseekV4Pro,
    DeepseekV4Flash,
    Glm52Coding,
    Glm47Coding,
    Glm52,
    Glm47,
    Glm52ZaiCoding,
    Glm47ZaiCoding,
    Glm52Zai,
    Glm47Zai,
    KimiK27Cn,
    KimiK25Cn,
    KimiK27Intl,
    KimiK25Intl
}

public static class ModelSiteExtensions
{
    /// <summary>Models offered for this site, in display order. First is the default.</summary>
    public static ModelChoice[] Models(this ModelSite s) => s switch
    {
        ModelSite.Deepseek     => new[] { ModelChoice.DeepseekV4Pro, ModelChoice.DeepseekV4Flash },
        ModelSite.Zhipu        => new[] { ModelChoice.Glm52Coding, ModelChoice.Glm47Coding, ModelChoice.Glm52, ModelChoice.Glm47 },
        ModelSite.ZhipuIntl    => new[] { ModelChoice.Glm52ZaiCoding, ModelChoice.Glm47ZaiCoding, ModelChoice.Glm52Zai, ModelChoice.Glm47Zai },
        ModelSite.Moonshot     => new[] { ModelChoice.KimiK27Cn, ModelChoice.KimiK25Cn },
        ModelSite.MoonshotIntl => new[] { ModelChoice.KimiK27Intl, ModelChoice.KimiK25Intl },
        _ => throw new ArgumentOutOfRangeException(nameof(s))
    };

    /// <summary>Resource key for the site label shown in the provider dropdown.</summary>
    public static string LabelKey(this ModelSite s) => s switch
    {
        ModelSite.Deepseek     => "Model_SiteDeepseek",
        ModelSite.Zhipu        => "Model_SiteZhipu",
        ModelSite.ZhipuIntl    => "Model_SiteZhipuIntl",
        ModelSite.Moonshot     => "Model_SiteMoonshot",
        ModelSite.MoonshotIntl => "Model_SiteMoonshotIntl",
        _ => throw new ArgumentOutOfRangeException(nameof(s))
    };

    /// <summary>Console page where the user creates an API key for this site.</summary>
    public static string ApiKeyUrl(this ModelSite s) => s switch
    {
        ModelSite.Deepseek     => "https://platform.deepseek.com/api_keys",
        ModelSite.Zhipu        => "https://bigmodel.cn/apikey/platform",
        ModelSite.ZhipuIntl    => "https://z.ai/manage-apikey/apikey-list",
        ModelSite.Moonshot     => "https://platform.moonshot.cn/console/api-keys",
        ModelSite.MoonshotIntl => "https://platform.moonshot.ai/console/api-keys",
        _ => throw new ArgumentOutOfRangeException(nameof(s))
    };

    /// <summary>Resource key for the "get your key from …" link text.</summary>
    public static string FooterLinkKey(this ModelSite s) => s switch
    {
        ModelSite.Deepseek     => "Model_Footer_Link_Deepseek",
        ModelSite.Zhipu        => "Model_Footer_Link_Zhipu",
        ModelSite.ZhipuIntl    => "Model_Footer_Link_ZhipuIntl",
        ModelSite.Moonshot     => "Model_Footer_Link_Moonshot",
        ModelSite.MoonshotIntl => "Model_Footer_Link_MoonshotIntl",
        _ => throw new ArgumentOutOfRangeException(nameof(s))
    };
}

public static class ModelChoiceExtensions
{
    public static string TemplateFile(this ModelChoice c) => c switch
    {
        ModelChoice.DeepseekV4Pro   => "deepseek-v4-pro.json",
        ModelChoice.DeepseekV4Flash => "deepseek-v4-flash.json",
        ModelChoice.Glm52Coding     => "glm-5.2-coding.json",
        ModelChoice.Glm47Coding     => "glm-4.7-coding.json",
        ModelChoice.Glm52           => "glm-5.2.json",
        ModelChoice.Glm47           => "glm-4.7.json",
        ModelChoice.Glm52ZaiCoding  => "glm-5.2-zai-coding.json",
        ModelChoice.Glm47ZaiCoding  => "glm-4.7-zai-coding.json",
        ModelChoice.Glm52Zai        => "glm-5.2-zai.json",
        ModelChoice.Glm47Zai        => "glm-4.7-zai.json",
        ModelChoice.KimiK27Cn       => "kimi-k2.7-cn.json",
        ModelChoice.KimiK25Cn       => "kimi-k2.5-cn.json",
        ModelChoice.KimiK27Intl     => "kimi-k2.7-intl.json",
        ModelChoice.KimiK25Intl     => "kimi-k2.5-intl.json",
        _ => throw new ArgumentOutOfRangeException(nameof(c))
    };

    public static string DisplayName(this ModelChoice c) => c switch
    {
        ModelChoice.DeepseekV4Pro   => "DeepSeek V4 Pro",
        ModelChoice.DeepseekV4Flash => "DeepSeek V4 Flash",
        ModelChoice.Glm52Coding     => "GLM-5.2 Coding Plan",
        ModelChoice.Glm47Coding     => "GLM-4.7 Coding Plan",
        ModelChoice.Glm52           => "GLM-5.2",
        ModelChoice.Glm47           => "GLM-4.7",
        ModelChoice.Glm52ZaiCoding  => "GLM-5.2 Coding Plan (z.ai)",
        ModelChoice.Glm47ZaiCoding  => "GLM-4.7 Coding Plan (z.ai)",
        ModelChoice.Glm52Zai        => "GLM-5.2 (z.ai)",
        ModelChoice.Glm47Zai        => "GLM-4.7 (z.ai)",
        ModelChoice.KimiK27Cn       => "Kimi K2.7 Code Highspeed",
        ModelChoice.KimiK25Cn       => "Kimi K2.5",
        ModelChoice.KimiK27Intl     => "Kimi K2.7 Code Highspeed (intl)",
        ModelChoice.KimiK25Intl     => "Kimi K2.5 (intl)",
        _ => throw new ArgumentOutOfRangeException(nameof(c))
    };

    /// <summary>Resource key for the per-model line shown in the model dropdown.</summary>
    public static string ItemKey(this ModelChoice c) => c switch
    {
        ModelChoice.DeepseekV4Pro   => "Model_Item_Pro",
        ModelChoice.DeepseekV4Flash => "Model_Item_Flash",
        ModelChoice.Glm52Coding     => "Model_Item_Glm52Coding",
        ModelChoice.Glm47Coding     => "Model_Item_Glm47Coding",
        ModelChoice.Glm52           => "Model_Item_Glm52",
        ModelChoice.Glm47           => "Model_Item_Glm47",
        ModelChoice.Glm52ZaiCoding  => "Model_Item_Glm52ZaiCoding",
        ModelChoice.Glm47ZaiCoding  => "Model_Item_Glm47ZaiCoding",
        ModelChoice.Glm52Zai        => "Model_Item_Glm52Zai",
        ModelChoice.Glm47Zai        => "Model_Item_Glm47Zai",
        ModelChoice.KimiK27Cn       => "Model_Item_KimiK27",
        ModelChoice.KimiK25Cn       => "Model_Item_KimiK25",
        ModelChoice.KimiK27Intl     => "Model_Item_KimiK27",
        ModelChoice.KimiK25Intl     => "Model_Item_KimiK25",
        _ => throw new ArgumentOutOfRangeException(nameof(c))
    };

    /// <summary>The site this model belongs to (used to restore site selection on re-entry).</summary>
    public static ModelSite Site(this ModelChoice c) => c switch
    {
        ModelChoice.DeepseekV4Pro or ModelChoice.DeepseekV4Flash => ModelSite.Deepseek,
        ModelChoice.Glm52Coding or ModelChoice.Glm47Coding or ModelChoice.Glm52 or ModelChoice.Glm47 => ModelSite.Zhipu,
        ModelChoice.Glm52ZaiCoding or ModelChoice.Glm47ZaiCoding or ModelChoice.Glm52Zai or ModelChoice.Glm47Zai => ModelSite.ZhipuIntl,
        ModelChoice.KimiK27Cn or ModelChoice.KimiK25Cn => ModelSite.Moonshot,
        ModelChoice.KimiK27Intl or ModelChoice.KimiK25Intl => ModelSite.MoonshotIntl,
        _ => throw new ArgumentOutOfRangeException(nameof(c))
    };
}
