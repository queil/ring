// ReSharper disable CollectionNeverUpdated.Global

namespace Queil.Ring.Configuration.Runnables;

public class Proc : RunnableConfigBase, IUseWorkingDir
{
    public string Command { get; init; } = string.Empty;
    public Dictionary<string, string> Env { get; } = new();
    public List<string> Args { get; } = [];
    public override string UniqueId => Id ?? Command;
    public string? WorkingDir { get; set; }
}
