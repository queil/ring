using System.Collections.Generic;
using Queil.Ring.Configuration.Runnables;

namespace Queil.Ring.Configuration.Interfaces;

public interface IRunnableConfig : IWorkspaceConfig
{
    string? FriendlyName { get; }
    List<string> Tags { get; }
    Dictionary<string, TaskDefinition> Tasks { get; }
}
