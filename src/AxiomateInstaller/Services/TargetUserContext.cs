namespace AxiomateInstaller.Services;

public static class TargetUserContext
{
    public static string? Sid { get; private set; }
    public static string? Profile { get; private set; }

    public static void Initialize(string? sid, string? profile)
    {
        Sid = string.IsNullOrWhiteSpace(sid) ? null : sid;
        Profile = string.IsNullOrWhiteSpace(profile) ? null : profile.TrimEnd('\\');
    }
}
