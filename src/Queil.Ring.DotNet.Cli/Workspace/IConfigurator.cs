namespace Queil.Ring.DotNet.Cli.Workspace;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Configuration;

public interface IConfigurator
{
    ConfigSet Current { get; }
    event EventHandler<ConfigurationChangedArgs> OnConfigurationChanged;
    Task LoadAsync(ConfiguratorPaths paths, CancellationToken token);
    Task UnloadAsync(CancellationToken token);
    bool TryGet(string key, [NotNullWhen(true)] out IRunnableConfig? cfg);
}
