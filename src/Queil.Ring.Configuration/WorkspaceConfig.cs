// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CollectionNeverUpdated.Global
namespace Queil.Ring.Configuration;

using System.Collections.Generic;
using System.Linq;
using Interfaces;
using Runnables;

public class WorkspaceConfig : IWorkspaceConfig
{
    public WorkspaceConfig? Parent { get; set; }
    public HashSet<string> DeclaredPaths { get; set; } = [];
    public string UniqueId => string.IsNullOrWhiteSpace(Path) ? "" : System.IO.Path.GetFullPath(Path);

    public string Path { get; set; } = string.Empty;
    public List<Proc> Proc { get; set; } = [];
    public List<AspNetCore> Aspnetcore { get; set; } = [];
    public List<IISExpress> Iisexpress { get; set; } = [];
    public List<IISXCore> Iisxcore { get; set; } = [];
    public List<NetExe> Netexe { get; set; } = [];
    public List<DockerCompose> Dockercompose { get; set; } = [];
    public List<Kustomize> Kustomize { get; set; } = [];
    public List<string> Imports { get; set; } = [];
    public List<WorkspaceConfig> Import { get; set; } = [];

    public IEnumerable<IRunnableConfig> All =>
        Proc.Union<IRunnableConfig>(Aspnetcore)
            .Union(Iisexpress)
            .Union(Iisxcore)
            .Union(Netexe)
            .Union(Dockercompose)
            .Union(Kustomize);
}