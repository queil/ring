using System.Collections.Generic;
using Queil.Ring.Configuration.Interfaces;

namespace Queil.Ring.Configuration.Runnables;

public class Proc : RunnableConfigBase, IUseWorkingDir
{
    public string Command { get; set; }
    public string WorkingDir { get; set; }
    public Dictionary<string, string> Env { get; set; } = new();
    public List<string> Args { get; set; } = new();
    public override string UniqueId => Id ?? Command;
}
