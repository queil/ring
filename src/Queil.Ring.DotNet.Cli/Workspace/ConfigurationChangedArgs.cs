namespace Queil.Ring.DotNet.Cli.Workspace;

using Configuration;
using System.Collections.Generic;

public class ConfigurationChangedArgs(IDictionary<string, IRunnableConfig> configuration)
{
    public IDictionary<string, IRunnableConfig> Configuration { get; } = configuration;
}