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
    MoonshotIntl,
    AliyunCn,
    AliyunCnCoding,
    AliyunCnToken,
    AliyunIntl,
    MimoCn,
    MimoIntl,
    MinimaxCn,
    MinimaxIntl
}

public enum ModelChoice
{
    // DeepSeek
    DeepseekV4Pro,
    DeepseekV4Flash,
    // Zhipu CN
    Glm52Coding,
    Glm47Coding,
    Glm52,
    Glm47,
    // Zhipu Intl
    Glm52ZaiCoding,
    Glm47ZaiCoding,
    Glm52Zai,
    Glm47Zai,
    // Moonshot
    KimiK27Cn,
    KimiK25Cn,
    KimiK27Intl,
    KimiK25Intl,
    // Aliyun CN (通用)
    QwenHighCn,
    QwenLowCn,
    // Aliyun CN (Coding Plan)
    QwenHighCnCoding,
    QwenLowCnCoding,
    // Aliyun CN (Token Plan)
    QwenHighCnToken,
    QwenLowCnToken,
    // Aliyun Intl (US 通用)
    QwenHighIntl,
    QwenLowIntl,
    // MiMo CN
    MimoProCn,
    MimoStdCn,
    // MiMo Intl
    MimoProIntl,
    MimoStdIntl,
    // MiniMax CN
    MinimaxHighCn,
    MinimaxLowCn,
    // MiniMax Intl
    MinimaxHighIntl,
    MinimaxLowIntl
}

public static class ModelSiteExtensions
{
    /// <summary>Models offered for this site, in display order. First is the default.</summary>
    public static ModelChoice[] Models(this ModelSite s) => s switch
    {
        ModelSite.Deepseek       => new[] { ModelChoice.DeepseekV4Pro, ModelChoice.DeepseekV4Flash },
        ModelSite.Zhipu          => new[] { ModelChoice.Glm52Coding, ModelChoice.Glm47Coding, ModelChoice.Glm52, ModelChoice.Glm47 },
        ModelSite.ZhipuIntl      => new[] { ModelChoice.Glm52ZaiCoding, ModelChoice.Glm47ZaiCoding, ModelChoice.Glm52Zai, ModelChoice.Glm47Zai },
        ModelSite.Moonshot       => new[] { ModelChoice.KimiK27Cn, ModelChoice.KimiK25Cn },
        ModelSite.MoonshotIntl   => new[] { ModelChoice.KimiK27Intl, ModelChoice.KimiK25Intl },
        ModelSite.AliyunCn       => new[] { ModelChoice.QwenHighCn, ModelChoice.QwenLowCn },
        ModelSite.AliyunCnCoding => new[] { ModelChoice.QwenHighCnCoding, ModelChoice.QwenLowCnCoding },
        ModelSite.AliyunCnToken  => new[] { ModelChoice.QwenHighCnToken, ModelChoice.QwenLowCnToken },
        ModelSite.AliyunIntl     => new[] { ModelChoice.QwenHighIntl, ModelChoice.QwenLowIntl },
        ModelSite.MimoCn         => new[] { ModelChoice.MimoProCn, ModelChoice.MimoStdCn },
        ModelSite.MimoIntl       => new[] { ModelChoice.MimoProIntl, ModelChoice.MimoStdIntl },
        ModelSite.MinimaxCn      => new[] { ModelChoice.MinimaxHighCn, ModelChoice.MinimaxLowCn },
        ModelSite.MinimaxIntl    => new[] { ModelChoice.MinimaxHighIntl, ModelChoice.MinimaxLowIntl },
        _ => throw new ArgumentOutOfRangeException(nameof(s))
    };

    /// <summary>Resource key for the site label shown in the provider dropdown.</summary>
    public static string LabelKey(this ModelSite s) => s switch
    {
        ModelSite.Deepseek       => "Model_SiteDeepseek",
        ModelSite.Zhipu          => "Model_SiteZhipu",
        ModelSite.ZhipuIntl      => "Model_SiteZhipuIntl",
        ModelSite.Moonshot       => "Model_SiteMoonshot",
        ModelSite.MoonshotIntl   => "Model_SiteMoonshotIntl",
        ModelSite.AliyunCn       => "Model_SiteAliyunCn",
        ModelSite.AliyunCnCoding => "Model_SiteAliyunCnCoding",
        ModelSite.AliyunCnToken  => "Model_SiteAliyunCnToken",
        ModelSite.AliyunIntl     => "Model_SiteAliyunIntl",
        ModelSite.MimoCn         => "Model_SiteMimoCn",
        ModelSite.MimoIntl       => "Model_SiteMimoIntl",
        ModelSite.MinimaxCn      => "Model_SiteMinimaxCn",
        ModelSite.MinimaxIntl    => "Model_SiteMinimaxIntl",
        _ => throw new ArgumentOutOfRangeException(nameof(s))
    };

    /// <summary>Console page where the user creates an API key for this site.</summary>
    public static string ApiKeyUrl(this ModelSite s) => s switch
    {
        ModelSite.Deepseek       => "https://platform.deepseek.com/api_keys",
        ModelSite.Zhipu          => "https://bigmodel.cn/apikey/platform",
        ModelSite.ZhipuIntl      => "https://z.ai/manage-apikey/apikey-list",
        ModelSite.Moonshot       => "https://platform.moonshot.cn/console/api-keys",
        ModelSite.MoonshotIntl   => "https://platform.moonshot.ai/console/api-keys",
        ModelSite.AliyunCn       => "https://bailian.console.aliyun.com/?tab=model#/api-key",
        ModelSite.AliyunCnCoding => "https://bailian.console.aliyun.com/?tab=model#/api-key",
        ModelSite.AliyunCnToken  => "https://bailian.console.aliyun.com/?tab=model#/api-key",
        ModelSite.AliyunIntl     => "https://modelstudio.console.alibabacloud.com/?tab=model#/api-key",
        ModelSite.MimoCn         => "https://platform.xiaomimimo.com/api-keys",
        ModelSite.MimoIntl       => "https://platform.xiaomimimo.com/api-keys",
        ModelSite.MinimaxCn      => "https://platform.minimaxi.com/user-center/basic-information/interface-key",
        ModelSite.MinimaxIntl    => "https://platform.minimax.io/user-center/basic-information/interface-key",
        _ => throw new ArgumentOutOfRangeException(nameof(s))
    };

    /// <summary>Resource key for the "get your key from …" link text.</summary>
    public static string FooterLinkKey(this ModelSite s) => s switch
    {
        ModelSite.Deepseek       => "Model_Footer_Link_Deepseek",
        ModelSite.Zhipu          => "Model_Footer_Link_Zhipu",
        ModelSite.ZhipuIntl      => "Model_Footer_Link_ZhipuIntl",
        ModelSite.Moonshot       => "Model_Footer_Link_Moonshot",
        ModelSite.MoonshotIntl   => "Model_Footer_Link_MoonshotIntl",
        ModelSite.AliyunCn       => "Model_Footer_Link_AliyunCn",
        ModelSite.AliyunCnCoding => "Model_Footer_Link_AliyunCnCoding",
        ModelSite.AliyunCnToken  => "Model_Footer_Link_AliyunCnToken",
        ModelSite.AliyunIntl     => "Model_Footer_Link_AliyunIntl",
        ModelSite.MimoCn         => "Model_Footer_Link_MimoCn",
        ModelSite.MimoIntl       => "Model_Footer_Link_MimoIntl",
        ModelSite.MinimaxCn      => "Model_Footer_Link_MinimaxCn",
        ModelSite.MinimaxIntl    => "Model_Footer_Link_MinimaxIntl",
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
        ModelChoice.KimiK27Cn         => "kimi-k2.7-cn.json",
        ModelChoice.KimiK25Cn         => "kimi-k2.5-cn.json",
        ModelChoice.KimiK27Intl       => "kimi-k2.7-intl.json",
        ModelChoice.KimiK25Intl       => "kimi-k2.5-intl.json",
        ModelChoice.QwenHighCn        => "qwen-high-cn.json",
        ModelChoice.QwenLowCn         => "qwen-low-cn.json",
        ModelChoice.QwenHighCnCoding  => "qwen-high-cn-coding.json",
        ModelChoice.QwenLowCnCoding   => "qwen-low-cn-coding.json",
        ModelChoice.QwenHighCnToken   => "qwen-high-cn-token.json",
        ModelChoice.QwenLowCnToken    => "qwen-low-cn-token.json",
        ModelChoice.QwenHighIntl      => "qwen-high-intl.json",
        ModelChoice.QwenLowIntl       => "qwen-low-intl.json",
        ModelChoice.MimoProCn         => "mimo-pro-cn.json",
        ModelChoice.MimoStdCn         => "mimo-std-cn.json",
        ModelChoice.MimoProIntl       => "mimo-pro-intl.json",
        ModelChoice.MimoStdIntl       => "mimo-std-intl.json",
        ModelChoice.MinimaxHighCn     => "minimax-high-cn.json",
        ModelChoice.MinimaxLowCn      => "minimax-low-cn.json",
        ModelChoice.MinimaxHighIntl   => "minimax-high-intl.json",
        ModelChoice.MinimaxLowIntl    => "minimax-low-intl.json",
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
        ModelChoice.KimiK27Cn         => "Kimi K2.7 Code Highspeed",
        ModelChoice.KimiK25Cn         => "Kimi K2.5",
        ModelChoice.KimiK27Intl       => "Kimi K2.7 Code Highspeed (intl)",
        ModelChoice.KimiK25Intl       => "Kimi K2.5 (intl)",
        ModelChoice.QwenHighCn        => "Qwen3.7 Max + Plus",
        ModelChoice.QwenLowCn         => "Qwen3.6 Plus + Flash",
        ModelChoice.QwenHighCnCoding  => "Qwen3.7 Max + Plus (Coding Plan)",
        ModelChoice.QwenLowCnCoding   => "Qwen3.6 Plus + Flash (Coding Plan)",
        ModelChoice.QwenHighCnToken   => "Qwen3.7 Max + Plus (Token Plan)",
        ModelChoice.QwenLowCnToken    => "Qwen3.6 Plus + Flash (Token Plan)",
        ModelChoice.QwenHighIntl      => "Qwen3.7 Max + Plus (intl)",
        ModelChoice.QwenLowIntl       => "Qwen3.6 Plus + Flash (intl)",
        ModelChoice.MimoProCn         => "MiMo V2.5 Pro",
        ModelChoice.MimoStdCn         => "MiMo V2.5",
        ModelChoice.MimoProIntl       => "MiMo V2.5 Pro (intl)",
        ModelChoice.MimoStdIntl       => "MiMo V2.5 (intl)",
        ModelChoice.MinimaxHighCn     => "MiniMax-M3",
        ModelChoice.MinimaxLowCn      => "MiniMax-M2.5",
        ModelChoice.MinimaxHighIntl   => "MiniMax-M3 (intl)",
        ModelChoice.MinimaxLowIntl    => "MiniMax-M2.5 (intl)",
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
        ModelChoice.KimiK27Cn         => "Model_Item_KimiK27",
        ModelChoice.KimiK25Cn         => "Model_Item_KimiK25",
        ModelChoice.KimiK27Intl       => "Model_Item_KimiK27",
        ModelChoice.KimiK25Intl       => "Model_Item_KimiK25",
        ModelChoice.QwenHighCn        => "Model_Item_QwenHigh",
        ModelChoice.QwenLowCn         => "Model_Item_QwenLow",
        ModelChoice.QwenHighCnCoding  => "Model_Item_QwenHigh",
        ModelChoice.QwenLowCnCoding   => "Model_Item_QwenLow",
        ModelChoice.QwenHighCnToken   => "Model_Item_QwenHigh",
        ModelChoice.QwenLowCnToken    => "Model_Item_QwenLow",
        ModelChoice.QwenHighIntl      => "Model_Item_QwenHigh",
        ModelChoice.QwenLowIntl       => "Model_Item_QwenLow",
        ModelChoice.MimoProCn         => "Model_Item_MimoPro",
        ModelChoice.MimoStdCn         => "Model_Item_MimoStd",
        ModelChoice.MimoProIntl       => "Model_Item_MimoPro",
        ModelChoice.MimoStdIntl       => "Model_Item_MimoStd",
        ModelChoice.MinimaxHighCn     => "Model_Item_MinimaxHigh",
        ModelChoice.MinimaxLowCn      => "Model_Item_MinimaxLow",
        ModelChoice.MinimaxHighIntl   => "Model_Item_MinimaxHigh",
        ModelChoice.MinimaxLowIntl    => "Model_Item_MinimaxLow",
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
        ModelChoice.QwenHighCn or ModelChoice.QwenLowCn => ModelSite.AliyunCn,
        ModelChoice.QwenHighCnCoding or ModelChoice.QwenLowCnCoding => ModelSite.AliyunCnCoding,
        ModelChoice.QwenHighCnToken or ModelChoice.QwenLowCnToken => ModelSite.AliyunCnToken,
        ModelChoice.QwenHighIntl or ModelChoice.QwenLowIntl => ModelSite.AliyunIntl,
        ModelChoice.MimoProCn or ModelChoice.MimoStdCn => ModelSite.MimoCn,
        ModelChoice.MimoProIntl or ModelChoice.MimoStdIntl => ModelSite.MimoIntl,
        ModelChoice.MinimaxHighCn or ModelChoice.MinimaxLowCn => ModelSite.MinimaxCn,
        ModelChoice.MinimaxHighIntl or ModelChoice.MinimaxLowIntl => ModelSite.MinimaxIntl,
        _ => throw new ArgumentOutOfRangeException(nameof(c))
    };
}
