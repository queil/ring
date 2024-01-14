// ReSharper disable CollectionNeverUpdated.Global

namespace Queil.Ring.Configuration.Runnables;

public abstract class CsProjRunnable : RunnableConfigBase, IUseCsProjFile, IFromGit
{
    private string? _id;
    public List<string> Args { get; init; } = [];
    public override string UniqueId => _id ??= Id ?? Path.GetFileNameWithoutExtension(Csproj);
    public string? SshRepoUrl { get; init; }
    public string? WorkingDir { get; set; }
    public string Csproj { get; set; } = string.Empty;
    public string Configuration { get; init; } = "Debug";
    public Dictionary<string, string> Env { get; init; } = new();
    public string FullPath => GetFullPath(WorkingDir, Csproj);

    public string LaunchSettingsJsonPath =>
        Path.Combine(Path.GetDirectoryName(FullPath) ?? string.Empty, "Properties/launchSettings.json");
}
