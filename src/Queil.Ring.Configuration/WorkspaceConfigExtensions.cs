namespace Queil.Ring.Configuration;

using System.Linq;

public static class WorkspaceConfigExtensions
{
    public static ConfigSet ToEffectiveConfig(this WorkspaceConfig root)
    {
        var (runnables, paths) = GetEffectiveConfig(root);
        return new ConfigSet(root.Path, runnables.ToDictionary(x => x.UniqueId, x => x), paths.ToArray());

        static (IEnumerable<IRunnableConfig> runnables, IEnumerable<string> importPaths) GetEffectiveConfig(
            WorkspaceConfig node)
        {
            var configs = node.All.DistinctBy(x => x.UniqueId).ToArray();
            var results = node.Import.Select(GetEffectiveConfig).ToList();
            return (configs.Concat(results.SelectMany(w => w.runnables).Except(configs)),
                results.SelectMany(w => w.importPaths).Concat([node.Path]));
        }
    }
}
