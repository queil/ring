using System.Threading;
using System.Threading.Tasks;
using Queil.Ring.DotNet.Cli.CsProj;
using Microsoft.Extensions.Logging;
using Queil.Ring.DotNet.Cli.Dtos;
using Queil.Ring.DotNet.Cli.Infrastructure;
using Queil.Ring.DotNet.Cli.Tools;
using NetExeConfig = Queil.Ring.Configuration.Runnables.NetExe;

namespace Queil.Ring.DotNet.Cli.Runnables.Windows.NetExe;

public class NetExeRunnable : CsProjRunnable<NetExeContext, NetExeConfig>
{
    private readonly ProcessRunner _processRunner;

    public NetExeRunnable(
        NetExeConfig config,
        ProcessRunner processRunner,
        ILogger<NetExeRunnable> logger,
        ISender sender) : base(config, logger, sender)
    {
        _processRunner = processRunner;
    }

    protected override NetExeContext CreateContext()
    {
        AddDetail(DetailsKeys.CsProjPath, Config.FullPath);
        var ctx = new NetExeContext
        {
            CsProjPath = Config.Csproj,
            WorkingDir = Config.GetWorkingDir(),
            EntryAssemblyPath = $"{Config.GetWorkingDir()}\\bin\\Debug\\{Config.GetProjName()}.exe"
        };

        AddDetail(DetailsKeys.WorkDir, ctx.WorkingDir);
        AddDetail(DetailsKeys.ProcessId, ctx.ProcessId);

        return ctx;
    }

    protected override async Task StartAsync(NetExeContext ctx, CancellationToken token)
    {
        _processRunner.Command = ctx.EntryAssemblyPath;
        var result = await _processRunner.RunProcessAsync(Config.Args.ToArray(), token);
        ctx.ProcessId = result.Pid;
        ctx.Output = result.Output;
    }
}
