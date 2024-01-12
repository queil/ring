using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Queil.Ring.Configuration;
using Queil.Ring.Configuration.Interfaces;

namespace Queil.Ring.DotNet.Cli.Workspace;

public interface IConfigurator
{
    event EventHandler<ConfigurationChangedArgs> OnConfigurationChanged;
    Task LoadAsync(ConfiguratorPaths paths, CancellationToken token);
    Task UnloadAsync(CancellationToken token);
    bool TryGet(string key, [NotNullWhen(true)] out IRunnableConfig? cfg);
    ConfigSet Current { get; }
}