namespace Queil.Ring.Configuration.Runnables;

public class DockerCompose : RunnableConfigBase, IUseWorkingDir, IFromGit
{
    public override string UniqueId => Id ?? Path;
    public string Path { get; init; } = string.Empty;
    public string FullPath => GetFullPath(WorkingDir, Path);
    public string? SshRepoUrl { get; init; }
    public string? WorkingDir { get; set; }
    public override bool Equals(object? obj) => obj is DockerCompose d && d.Path == Path;
    public override int GetHashCode() => -576574704 + Path.GetHashCode();
}
