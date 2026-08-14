// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable ReturnTypeCanBeEnumerable.Global

namespace Queil.Ring.Configuration;

using System.Linq;
using Runnables;

public class WorkspaceConfig : IWorkspaceConfig
{
    public WorkspaceConfig? Parent { get; set; }
    public string Path { get; set; } = string.Empty;
    public Dictionary<string, Dictionary<string, string>> Env { get; init; } = [];
    public Dictionary<string, Dictionary<string, TaskDefinition>> Tasks { get; } = [];
    public List<Proc> Proc { get; } = [];
    public List<Dotnet> Dotnet { get; } = [];
    public List<DockerCompose> Dockercompose { get; } = [];
    public List<Kustomize> Kustomize { get; } = [];
    public List<string> Imports { get; } = [];
    public List<WorkspaceConfig> Import { get; } = [];

    public IEnumerable<IRunnableConfig> All =>
        Proc.Union<IRunnableConfig>(Dotnet)
            .Union(Dockercompose)
            .Union(Kustomize);

    public HashSet<string> DeclaredPaths { get; set; } = [];
    public string UniqueId => string.IsNullOrWhiteSpace(Path) ? "" : System.IO.Path.GetFullPath(Path);
}
