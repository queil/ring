namespace Queil.Ring.DotNet.Cli.Workspace;

using System.Threading;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tools;

public class WorkspaceInitHook : IWorkspaceInitHook
{
    private readonly bool _configured;
    private readonly ILogger<WorkspaceInitHook> _logger;
    private readonly ProcessRunner _runner;

    public WorkspaceInitHook(ILogger<WorkspaceInitHook> logger, ProcessRunner runner, IOptions<RingConfiguration> opts)
    {
        _logger = logger;
        _runner = runner;
        var config = opts.Value?.Hooks?.Init;
        if (config is not { Command: { } c, Args: { } args }) return;
        _configured = true;
        _runner.Command = c;
        _runner.DefaultArgs = args;
    }

    public async Task RunAsync(CancellationToken token)
    {
        if (_configured)
        {
            _logger.LogDebug("Executing Workspace Init Hook");
            await _runner.RunProcessWaitAsync(token);
        }
        else
        {
            _logger.LogDebug("Workspace Init Hook not configured. Skipping.");
        }
    }
}
