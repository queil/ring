using System.Collections.Generic;
using System.Linq;
using Queil.Ring.Configuration.Interfaces;
using Queil.Ring.Configuration.Runnables;
using ATech.Ring.DotNet.Cli.Dtos;

namespace ATech.Ring.DotNet.Cli.Abstractions;

public static class DetailsExtractors
{
    public static RunnableDetails Extract(IRunnableConfig cfg)
    {
        static Dictionary<string,object> New(params (string key, object value)[] details) => details.ToDictionary(x => x.key, x => x.value);
        var details = cfg switch
        {
            CsProjRunnable c => New((DetailsKeys.CsProjPath, c.FullPath)),
            _ => new Dictionary<string,object>()
        };

        if (cfg.FriendlyName != null) details.Add(DetailsKeys.FriendlyName, cfg.FriendlyName);
        return new RunnableDetails(details);
    }
}
