namespace Queil.Ring.DotNet.Cli.Abstractions;

using System.Collections.Generic;
using System.Linq;
using Configuration;
using Configuration.Runnables;
using Dtos;
using RunnableDetails = System.Collections.ObjectModel.ReadOnlyDictionary<string, object>;

public static class DetailsExtractors
{
    public static RunnableDetails Extract(IRunnableConfig cfg)
    {
        var details = cfg switch
        {
            CsProjRunnable c => New((DetailsKeys.CsProjPath, c.FullPath)),
            _ => []
        };

        if (cfg.FriendlyName != null) details.Add(DetailsKeys.FriendlyName, cfg.FriendlyName);
        return new RunnableDetails(details);

        static Dictionary<string, object> New(params (string key, object value)[] details)
        {
            return details.ToDictionary(x => x.key, x => x.value);
        }
    }
}
