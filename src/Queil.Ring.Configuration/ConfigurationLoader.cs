namespace Queil.Ring.Configuration;

using Tomlyn;

public class ConfigurationLoader : IConfigurationLoader
{
    private readonly TomlModelOptions _options = new();

    public ConfigurationLoader()
    {
        _options.ConvertPropertyName = name => char.ToLower(name[0]) + name[1..];
    }

    public T Load<T>(string path) where T : class, new()
    {
        var text = File.ReadAllText(path);
        if (typeof(T) == typeof(WorkspaceConfig)) LegacyRunnableTypes.Validate(Toml.ToModel(text, path), path);
        return Toml.ToModel<T>(text, path, _options);
    }
}
