namespace Queil.Ring.Configuration;

public class ConfigurationTreeReader(IConfigurationLoader loader) : IConfigurationTreeReader
{
    public WorkspaceConfig GetConfigTree(ConfiguratorPaths paths)
    {
        var file = new FileInfo(Path.GetFullPath(paths.WorkspacePath));
        var rootDir = file.DirectoryName;
        return Populate(file.Name, null, rootDir ?? string.Empty);

        WorkspaceConfig Populate(string path, WorkspaceConfig? parent, string currentDirectory)
        {
            var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(currentDirectory, path);
            var c = loader.Load<WorkspaceConfig>(fullPath);
            foreach (var import in c.Imports) c.Import.Add(new WorkspaceConfig { Path = import });
            c.Parent = parent;
            c.Path = fullPath;

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

                r.DeclaredPaths.Add(fullPath);
            }

            for (var i = 0; i < c.Import.Count; i++)
                c.Import[i] = Populate(c.Import[i].Path, c, new FileInfo(fullPath).DirectoryName ?? string.Empty);
            return c;
        }
    }
}
