using Nett;
using Queil.Ring.Configuration.Interfaces;

namespace Queil.Ring.Configuration;

public class ConfigurationLoader : IConfigurationLoader
{
    public T Load<T>(string path) => Toml.ReadFile<T>(path, TomlConfig.Settings);
}
