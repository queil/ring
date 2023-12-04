using System.Collections.Generic;

namespace Queil.Ring.Configuration.Interfaces;

public interface IWorkspaceConfig
{
    string UniqueId { get; }
    HashSet<string> DeclaredPaths { get; set; }
}
