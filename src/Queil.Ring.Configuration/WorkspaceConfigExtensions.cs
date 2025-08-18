namespace Queil.Ring.Configuration;

using System.Linq;

public static class WorkspaceConfigExtensions
{
    public static ConfigSet ToEffectiveConfig(this WorkspaceConfig root)
    {
        return new ConfigSet(root.Path, GetEffectiveConfig(root).ToDictionary(x => x.UniqueId, x => x));

        static IEnumerable<IRunnableConfig> GetEffectiveConfig(WorkspaceConfig node)
        {
            var configs = node.All.DistinctBy(x => x.UniqueId).ToArray();
            var nested = (from w in node.Import select GetEffectiveConfig(w)).SelectMany(w => w).ToArray();
            return configs.Concat(nested.Except(configs));
        }
    }
}
