using System.IO;
using Queil.Ring.Configuration.Interfaces;
using Tomlyn;

namespace Queil.Ring.Configuration;

public class ConfigurationLoader : IConfigurationLoader
{
    private readonly TomlModelOptions _options = new();
    public ConfigurationLoader()
    {
        _options.ConvertPropertyName = name => char.ToLower(name[0]) + name[1..];
    }

    public T Load<T>(string path) where T : class, new() => Toml.ToModel<T>(File.ReadAllText(path), path, _options);
}
