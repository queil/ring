using System.Collections.Generic;
using System.Linq;
using Queil.Ring.Configuration.Interfaces;
using Queil.Ring.Configuration.Runnables;

namespace Queil.Ring.Configuration;

public class WorkspaceConfig : IWorkspaceConfig
{
    public WorkspaceConfig? Parent { get; set; }

    public string UniqueId => string.IsNullOrWhiteSpace(path) ? "" : System.IO.Path.GetFullPath(path);
    public HashSet<string> DeclaredPaths { get; set; } = new();

    public string path { get; set; }
    public List<Proc> proc { get; set; } = new();
    public List<AspNetCore> aspnetcore { get; set; } = new();
    public List<IISExpress> iisexpress { get; set; } = new();
    public List<IISXCore> iisxcore { get; set; } = new();
    public List<NetExe> netexe { get; set; } = new();
    public List<DockerCompose> dockercompose { get; set; } = new();
    public List<Kustomize> kustomize { get; set; } = new();
    public List<string> imports { get; set; } = new();
    public List<WorkspaceConfig> import { get; set; } = new();

    public IEnumerable<IRunnableConfig> All =>
        proc.Union<IRunnableConfig>(aspnetcore)
            .Union(iisexpress)
            .Union(iisxcore)
            .Union(netexe)
            .Union(dockercompose)
            .Union(kustomize);
}