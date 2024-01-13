using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Queil.Ring.DotNet.Cli.Abstractions.Context;
using Queil.Ring.DotNet.Cli.Infrastructure;
using Queil.Ring.DotNet.Cli.Tools;

namespace Queil.Ring.DotNet.Cli.Runnables.Proc;

public class ProcContext : ITrackProcessId
{
    public int ProcessId { get; set; }
}

public class ProcRunnable : ProcessRunnable<ProcContext, Queil.Ring.Configuration.Runnables.Proc>
{
    private readonly ProcessRunner _runner;

    public ProcRunnable(Queil.Ring.Configuration.Runnables.Proc config,
        ILogger<ProcessRunnable<ProcContext, Queil.Ring.Configuration.Runnables.Proc>> logger,
        ISender sender,
        ProcessRunner runner) : base(config, logger, sender)
    {
        _runner = runner;
        _runner.Command = config.Command;
    }

    public override string UniqueId => Config.UniqueId;

    protected override Task<ProcContext> InitAsync(CancellationToken token) => Task.FromResult(new ProcContext());

    protected override async Task StartAsync(ProcContext ctx, CancellationToken token)
    {
        var info = await _runner.RunProcessAsync(Config.WorkingDir, Config.Env, Config.Args.ToArray(), token);
        ctx.ProcessId = info.Pid;
    }
}
