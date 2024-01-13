using System.Collections.ObjectModel;

namespace Queil.Ring.DotNet.Cli.Abstractions;

using Dtos;
using Configuration;
using Queil.Ring.Configuration.Runnables;
using System.Collections.Generic;
using System.Linq;
using RunnableDetails = ReadOnlyDictionary<string, object>; 

public static class DetailsExtractors
{
    public static RunnableDetails Extract(IRunnableConfig cfg)
    {
        var details = cfg switch
        {
            CsProjRunnable c => New((DetailsKeys.CsProjPath, c.FullPath)),
            _ => new Dictionary<string, object>()
        };

        if (cfg.FriendlyName != null) details.Add(DetailsKeys.FriendlyName, cfg.FriendlyName);
        return new RunnableDetails(details);
        
        static Dictionary<string, object> New(params (string key, object value)[] details) => details.ToDictionary(x => x.key, x => x.value);
    }
}
