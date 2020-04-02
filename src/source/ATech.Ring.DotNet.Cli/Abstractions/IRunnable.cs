using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ATech.Ring.Configuration.Interfaces;

namespace ATech.Ring.DotNet.Cli.Abstractions
{
    public interface IRunnable
    {
        Task RunAsync(IRunnableConfig config, CancellationToken token);
        Task TerminateAsync(CancellationToken token);
        string UniqueId { get; }
        State State { get; }
        event EventHandler OnHealthCheckCompleted;
        IReadOnlyDictionary<string,object> Details { get; }
    }
}