using NetExeConfig = Queil.Ring.Configuration.Runnables.NetExe;

namespace Queil.Ring.DotNet.Cli.Runnables.Windows.NetExe;

using System.Threading;
using System.Threading.Tasks;
using CsProj;
using Dtos;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Tools;

public class NetExeApp(
    NetExeConfig config,
    ProcessRunner processRunner,
    ILogger<NetExeApp> logger,
    ISender sender)
    : CsProjApp<NetExeContext, NetExeConfig>(config, logger, sender)
{
    protected override NetExeContext CreateContext()
    {
        AddDetail(DetailsKeys.CsProjPath, Config.FullPath);
        var ctx = new NetExeContext
        {
            CsProjPath = Config.Csproj,
            WorkingDir = Config.GetWorkingDir(),
            EntryAssemblyPath = $@"{Config.GetWorkingDir()}\bin\Debug\{Config.GetProjName()}.exe"
        };

        AddDetail(DetailsKeys.WorkDir, ctx.WorkingDir);
        AddDetail(DetailsKeys.ProcessId, ctx.ProcessId);

        return ctx;
    }

    protected override async Task StartAsync(NetExeContext ctx, CancellationToken token)
    {
        processRunner.Command = ctx.EntryAssemblyPath;
        var result = await processRunner.RunAsync(Config.Args, token: token);
        ctx.ProcessId = result.Pid;
        ctx.Output = result.Output;
    }
}
