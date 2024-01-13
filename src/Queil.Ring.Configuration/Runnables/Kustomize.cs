namespace Queil.Ring.Configuration.Runnables;

public class Kustomize : RunnableConfigBase, IUseWorkingDir, IFromGit
{
    public override string UniqueId => Id ?? Path;
    public string Path { get; init; } = string.Empty;
    public string? WorkingDir { get; set; }
    public string? SshRepoUrl { get; init; }
    public string FullPath => GetFullPath(WorkingDir, Path);
    public bool IsRemote() => Path.StartsWith("git@") || Path.StartsWith("ssh://");

    public override bool Equals(object? obj) => obj is Kustomize d && d.Path == Path;
    public override int GetHashCode() => -576574704 + Path.GetHashCode();
    
}
