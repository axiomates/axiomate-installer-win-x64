using System;
using System.IO;

namespace AxiomateInstaller.Services;

/// <summary>
/// Writes a site-level pip configuration to the bundled Python install,
/// so every user on this machine using that interpreter picks the mirror.
/// User-level %APPDATA%\pip\pip.ini still wins, allowing per-user override.
/// </summary>
public sealed class PipMirrorWriter
{
    private readonly Logger _log;
    public PipMirrorWriter(Logger log) { _log = log; }

    public void Write(string pythonInstallDir, PipMirror mirror)
    {
        string path = Path.Combine(pythonInstallDir, "pip.ini");
        if (File.Exists(path))
        {
            try
            {
                File.Copy(path, path + ".bak", overwrite: true);
                _log.Info($"Existing pip.ini backed up to {path}.bak");
            }
            catch (Exception ex)
            {
                _log.Warn($"Could not back up existing pip.ini: {ex.Message}");
            }
        }

        string content =
            "[global]\r\n" +
            $"index-url = {mirror.IndexUrl()}\r\n" +
            $"trusted-host = {mirror.TrustedHost()}\r\n";

        Directory.CreateDirectory(pythonInstallDir);
        File.WriteAllText(path, content);
        _log.Info($"Wrote pip mirror config: {path} -> {mirror.DisplayName()}");
    }
}
