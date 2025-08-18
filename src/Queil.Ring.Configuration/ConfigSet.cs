namespace Queil.Ring.Configuration;

using System.Linq;

public class ConfigSet : Dictionary<string, IRunnableConfig>
{
    public const string AllFlavours = "__all";

    public static readonly ConfigSet Empty = new(string.Empty, [],[]);

    public ConfigSet(string rootPath, Dictionary<string, IRunnableConfig> bareConfigs, string[] allPaths)
    {
        foreach (var (key, value) in bareConfigs)
        {
            value.Tags.Add(AllFlavours);
            Add(key, value);
        }

        RootPath = rootPath;
        Flavours = [.. bareConfigs.Values.SelectMany(x => x.Tags)];
        AllPaths = new HashSet<string>(allPaths);
    }

    public HashSet<string> Flavours { get; }
    public string RootPath { get; }
    public HashSet<string> AllPaths { get; }
}
