namespace Queil.Ring.DotNet.Cli.Abstractions;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IRunnable
{
    string UniqueId { get; }
    State State { get; }
    IReadOnlyDictionary<string, object> Details { get; }
    Task ConfigureAsync(CancellationToken token);
    Task RunAsync(CancellationToken token);
    Task TerminateAsync();
    event EventHandler OnHealthCheckCompleted;
    event EventHandler OnInitExecuted;
}
