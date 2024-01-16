namespace Queil.Ring.DotNet.Cli.Runnables.Proc;

using System.Threading;
using System.Threading.Tasks;
using Abstractions.Context;
using Configuration.Runnables;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Tools;

public class ProcContext : ITrackProcessId
{
    public int ProcessId { get; set; }
}

public class ProcApp : ProcessApp<ProcContext, Proc>
{
    private readonly ProcessRunner _runner;

    public ProcApp(Proc config,
        ILogger<ProcessApp<ProcContext, Proc>> logger,
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
        var info = await _runner.RunAsync(Config.Args, Config.WorkingDir, Config.Env, token: token);
        ctx.ProcessId = info.Pid;
    }
}
