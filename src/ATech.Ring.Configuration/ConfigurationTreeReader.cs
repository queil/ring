﻿using System.IO;
using ATech.Ring.Configuration.Interfaces;

namespace ATech.Ring.Configuration;

public class ConfigurationTreeReader : IConfigurationTreeReader
{
    private readonly IConfigurationLoader _loader;

    public ConfigurationTreeReader(IConfigurationLoader loader)
    {
        _loader = loader;
    }

    public WorkspaceConfig GetConfigTree(ConfiguratorPaths paths)
    {
        var file = new FileInfo(Path.GetFullPath(paths.WorkspacePath));

        var rootDir = file.DirectoryName;

        return Populate(file.Name, null, rootDir);

        WorkspaceConfig Populate(string path, WorkspaceConfig parent, string currentDirectory)
        {
            var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(currentDirectory, path);
            var c = _loader.Load<WorkspaceConfig>(fullPath);
            if (c == null) return new WorkspaceConfig();
            foreach (var import in c.imports)
            {
                c.import.Add(new WorkspaceConfig{path = import});
            }
            c.Parent = parent;
            c.path = fullPath;

            foreach (var r in c.Elements<IRunnableConfig>())
            {
                if (r is IUseWorkingDir wd)
                {
                    var defaultWd = new FileInfo(c.path).DirectoryName ?? string.Empty;
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
            for (var i = 0; i < c.import.Count; i++)
            {
                c.import[i] = Populate(c.import[i].path, c, new FileInfo(fullPath).DirectoryName);
            }
            return c;
        }
    }
}
