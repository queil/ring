using System.Collections.Generic;
using System.IO;
using Queil.Ring.Configuration.Interfaces;

namespace Queil.Ring.Configuration.Runnables;

public abstract class CsProjRunnable : RunnableConfigBase, IUseCsProjFile, IFromGit
{
    public string? WorkingDir { get; set; }
    public string Csproj { get; set; }
    public string SshRepoUrl { get; set; }
    public string Configuration { get; set; } = "Debug";
    public Dictionary<string, string> Env { get; set; } = new();
    public List<string> Args { get; set; } = new();
    public string FullPath => GetFullPath(WorkingDir, Csproj);
    
    public string LaunchSettingsJsonPath => Path.Combine(Path.GetDirectoryName(FullPath), "Properties/launchSettings.json");
    private string? _id;
    public override string UniqueId => _id ??= Id ?? Path.GetFileNameWithoutExtension(Csproj);
}
