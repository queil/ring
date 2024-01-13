using Queil.Ring.Configuration;

namespace Queil.Ring.DotNet.Cli.Workspace;

using System.Collections.Generic;

public class ConfigurationChangedArgs
{
    public IDictionary<string, IRunnableConfig> Configuration { get; }
    public ConfigurationChangedArgs(IDictionary<string, IRunnableConfig> configuration) => Configuration = configuration;
}