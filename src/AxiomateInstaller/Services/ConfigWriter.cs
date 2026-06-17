using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace AxiomateInstaller.Services;

/// <summary>
/// Materializes a model template from embedded resources, substitutes the
/// API key, and writes it to ~/.axiomate.json. The user has explicitly
/// opted into this clean overwrite via the wizard checkbox; we also wipe
/// ~/.axiomate/ so prior session data won't reference removed models.
/// </summary>
public sealed class ConfigWriter
{
    private readonly Logger _log;
    public ConfigWriter(Logger log) { _log = log; }

    public void Write(ModelChoice choice, string apiKey)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) throw new InstallStepException(Strings.Get("Err_Cfg_NoHome"));

        string axiomateDir = Path.Combine(home, ".axiomate");
        string configPath  = Path.Combine(home, ".axiomate.json");

        if (Directory.Exists(axiomateDir))
        {
            _log.Info($"Removing existing {axiomateDir} (user opted into clean overwrite).");
            try
            {
                ForceDelete(axiomateDir);
            }
            catch (Exception ex)
            {
                _log.Warn($"Could not delete {axiomateDir}: {ex.Message} — continuing anyway.");
            }
        }

        string templateName = choice.TemplateFile();
        string content = LoadTemplate(templateName);
        content = content.Replace("{{API_KEY}}", JsonStringEscape(apiKey));

        File.WriteAllText(configPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _log.Info($"Wrote {configPath} from template {templateName}");
    }

    private static string LoadTemplate(string fileName)
    {
        Assembly asm = typeof(ConfigWriter).Assembly;
        string asmName = asm.GetName().Name ?? "AxiomateInstaller";
        string resourceName = $"{asmName}.Templates.{fileName}";
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

    private static void ForceDelete(string dir)
    {
        foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
        }
        Directory.Delete(dir, recursive: true);
    }
}
