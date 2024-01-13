// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable ReturnTypeCanBeEnumerable.Global
namespace Queil.Ring.Configuration;

using System.Linq;
using Runnables;

public class WorkspaceConfig : IWorkspaceConfig
{
    public WorkspaceConfig? Parent { get; set; }
    public HashSet<string> DeclaredPaths { get; set; } = [];
    public string UniqueId => string.IsNullOrWhiteSpace(Path) ? "" : System.IO.Path.GetFullPath(Path);

    public string Path { get; set; } = string.Empty;
    public List<Proc> Proc { get; } = [];
    public List<AspNetCore> Aspnetcore { get; } = [];
    public List<IISExpress> Iisexpress { get; } = [];
    public List<IISXCore> Iisxcore { get; } = [];
    public List<NetExe> Netexe { get; } = [];
    public List<DockerCompose> Dockercompose { get; } = [];
    public List<Kustomize> Kustomize { get; } = [];
    public List<string> Imports { get; } = [];
    public List<WorkspaceConfig> Import { get; } = [];

    public IEnumerable<IRunnableConfig> All =>
        Proc.Union<IRunnableConfig>(Aspnetcore)
            .Union(Iisexpress)
            .Union(Iisxcore)
            .Union(Netexe)
            .Union(Dockercompose)
            .Union(Kustomize);
}