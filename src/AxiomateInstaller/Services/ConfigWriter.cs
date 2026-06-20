using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace AxiomateInstaller.Services;

/// <summary>
/// Materializes a model template from embedded resources, substitutes the
/// API key, and writes it to ~/.axiomate.json.
/// </summary>
public sealed class ConfigWriter
{
    private readonly Logger _log;
    public ConfigWriter(Logger log) { _log = log; }

    public void Write(ModelChoice choice, string apiKey)
    {
        string home = UserProfileResolver.GetUserProfile(_log);
        if (string.IsNullOrEmpty(home)) throw new InstallStepException(Strings.Get("Err_Cfg_NoHome"));

        string configPath  = Path.Combine(home, ".axiomate.json");

        string templateName = choice.TemplateFile();
        string content = LoadTemplate(templateName);
        content = content.Replace("{{API_KEY}}", JsonStringEscape(apiKey));

        File.WriteAllText(configPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _log.Info($"Wrote {configPath} from template {templateName}");
    }

    private static string LoadTemplate(string fileName)
    {
        Assembly asm = typeof(ConfigWriter).Assembly;
        // RootNamespace, not AssemblyName — see PayloadExtractor for the same caveat.
        string resourceName = $"AxiomateInstaller.Templates.{fileName}";
        using Stream? s = asm.GetManifestResourceStream(resourceName)
            ?? throw new InstallStepException(Strings.Format("Err_Cfg_TmplMissing_Format", fileName));
        using var sr = new StreamReader(s);
        return sr.ReadToEnd();
    }

    /// <summary>Escape user-supplied API key safely for embedding in JSON.</summary>
    private static string JsonStringEscape(string raw)
    {
        var sb = new StringBuilder(raw.Length + 8);
        foreach (char c in raw)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append($"\\u{(int)c:X4}");
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
