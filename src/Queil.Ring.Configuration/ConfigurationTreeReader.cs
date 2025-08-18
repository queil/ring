namespace Queil.Ring.Configuration;

using System.Linq;
using Runnables;

public class ConfigurationTreeReader(IConfigurationLoader loader) : IConfigurationTreeReader
{
    public WorkspaceConfig GetConfigTree(ConfiguratorPaths paths)
    {
        var file = new FileInfo(Path.GetFullPath(paths.WorkspacePath));
        var rootDir = file.DirectoryName;
        return Populate(file.Name, null, rootDir ?? string.Empty, null, null);

        WorkspaceConfig Populate(string path, WorkspaceConfig? parent, string currentDirectory,
            Dictionary<string, Dictionary<string, string>>? envsByRunnableType,
            Dictionary<string, Dictionary<string, TaskDefinition>>? tasksByRunnableType)
        {
            var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(currentDirectory, path);
            var c = loader.Load<WorkspaceConfig>(fullPath);
            foreach (var import in c.Imports) c.Import.Add(new WorkspaceConfig { Path = import });
            c.Parent = parent;
            c.Path = fullPath;

            var mergedEnvs = (envsByRunnableType ?? []).DeepMerge(c.Env);
            var mergedTasks = (tasksByRunnableType ?? []).DeepMerge(c.Tasks);

            foreach (var r in c.All)
            {
                if (r is IUseWorkingDir wd)
                {
                    var defaultWd = new FileInfo(c.Path).DirectoryName ?? string.Empty;
                    var newWorkingDir = wd.WorkingDir switch
                    {
                        null => defaultWd,
                        var dir when Path.IsPathRooted(dir) => dir,
                        var dir => Path.Combine(defaultWd, dir)
                    };
                    wd.WorkingDir = Path.GetFullPath(newWorkingDir);
                }

                if (mergedEnvs.TryGetValue(r.TypeId, out var envs))
                {
                    foreach (var (k, v) in envs.Where(x => !r.Env.ContainsKey(x.Key)))
                    {
                        r.Env[k] = v;
                    }
                }

                if (mergedTasks.TryGetValue(r.TypeId, out var tasks))
                {
                    foreach (var (k, v) in tasks.Where(x => !r.Tasks.ContainsKey(x.Key)))
                    {
                        r.Tasks[k] = v;
                    }
                }

                r.DeclaredPaths.Add(fullPath);
            }

            for (var i = 0; i < c.Import.Count; i++)
                c.Import[i] = Populate(c.Import[i].Path, c, new FileInfo(fullPath).DirectoryName ?? string.Empty,
                    mergedEnvs, mergedTasks);
            return c;
        }
    }
}
