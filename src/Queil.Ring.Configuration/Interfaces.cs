namespace Queil.Ring.Configuration;

using Runnables;

public interface IConfigurationLoader
{
    T Load<T>(string path) where T : class, new();
}

public interface IConfigurationTreeReader
{
    WorkspaceConfig GetConfigTree(ConfiguratorPaths paths);
}

public interface IFromGit
{
    string? SshRepoUrl { get; }
}

public interface IRunnableConfig : IWorkspaceConfig
{
    string? FriendlyName { get; }
    List<string> Tags { get; }
    Dictionary<string, TaskDefinition> Tasks { get; }
}

public interface IUseCsProjFile : IUseWorkingDir
{
    string Csproj { get; set; }
    string FullPath { get; }
    string LaunchSettingsJsonPath { get; }
    string Configuration { get; }
    public Dictionary<string, string> Env { get; }
}

public interface IUseWorkingDir
{
    string? WorkingDir { get; set; }
}

public interface IWorkspaceConfig
{
    string UniqueId { get; }
    HashSet<string> DeclaredPaths { get; }
}
