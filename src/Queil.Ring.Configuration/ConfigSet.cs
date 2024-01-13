namespace Queil.Ring.Configuration;

using System.Collections.Generic;
using System.Linq;
using Interfaces;

public class ConfigSet : Dictionary<string, IRunnableConfig>
{
    public const string AllFlavours = "__all";
    public HashSet<string> Flavours { get; }
    public string Path { get; }
    public ConfigSet(string path, Dictionary<string, IRunnableConfig> bareConfigs)
    {
        foreach (var (key, value) in bareConfigs)
        {
            value.Tags.Add(AllFlavours);
            Add(key, value);
        }
        Path = path;
        Flavours = [..bareConfigs.Values.SelectMany(x => x.Tags)];
    }

    public static readonly ConfigSet Empty = new(string.Empty, []);
}
