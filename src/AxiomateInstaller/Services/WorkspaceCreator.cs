using System.IO;

namespace AxiomateInstaller.Services;

/// <summary>
/// Creates the chosen workspace directory. Launching is handled programmatically
/// through Windows Terminal/direct process shortcuts rather than a .cmd script.
/// </summary>
public sealed class WorkspaceCreator
{
    private readonly Logger _log;
    public WorkspaceCreator(Logger log) { _log = log; }

    public string CreateWorkspace(string workspaceDir, string userProfile)
    {
        string resolved = WorkspacePathGuard.ResolveForUser(workspaceDir, userProfile);
        Directory.CreateDirectory(resolved);
        _log.Info($"Created workspace: {resolved}");
        return resolved;
    }
}
