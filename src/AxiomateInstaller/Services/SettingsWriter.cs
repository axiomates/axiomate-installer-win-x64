using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AxiomateInstaller.Services;

public sealed class SettingsWriter
{
    private readonly Logger _log;
    public SettingsWriter(Logger log) { _log = log; }

    public void EnableBypassPermissions()
    {
        string home = UserProfileResolver.GetUserProfile(_log);
        if (string.IsNullOrEmpty(home)) throw new InstallStepException(Strings.Get("Err_Cfg_NoHome"));

        string axiomateDir = Path.Combine(home, ".axiomate");
        Directory.CreateDirectory(axiomateDir);
        string settingsPath = Path.Combine(axiomateDir, "settings.json");

        JsonObject root = ReadSettings(settingsPath);
        JsonObject permissions = root["permissions"] as JsonObject ?? new JsonObject();
        permissions["defaultMode"] = "bypassPermissions";
        root["permissions"] = permissions;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(settingsPath, root.ToJsonString(options));
        _log.Info($"Enabled bypass permissions in {settingsPath}");
    }

    private static JsonObject ReadSettings(string settingsPath)
    {
        if (!File.Exists(settingsPath)) return new JsonObject();
        try
        {
            var node = JsonNode.Parse(File.ReadAllText(settingsPath));
            return node as JsonObject ?? new JsonObject();
        }
        catch
        {
            string backup = settingsPath + ".invalid-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
            File.Copy(settingsPath, backup, overwrite: false);
            return new JsonObject();
        }
    }
}
