namespace Queil.Ring.DotNet.Cli.Workspace;

using System.Collections.Generic;
using Configuration;

public class ConfigurationChangedArgs(IDictionary<string, IRunnableConfig> configuration)
{
    public IDictionary<string, IRunnableConfig> Configuration { get; } = configuration;
}
