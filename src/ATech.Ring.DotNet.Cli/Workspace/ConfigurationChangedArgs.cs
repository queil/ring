namespace ATech.Ring.DotNet.Cli.Workspace;

using System.Collections.Generic;
using Queil.Ring.Configuration.Interfaces;

public class ConfigurationChangedArgs
{
    public IDictionary<string, IRunnableConfig> Configuration { get; }
    public ConfigurationChangedArgs(IDictionary<string, IRunnableConfig> configuration) => Configuration = configuration;       
}