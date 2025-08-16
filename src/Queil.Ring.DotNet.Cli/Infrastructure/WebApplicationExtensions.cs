namespace Queil.Ring.DotNet.Cli.Infrastructure;

using System;
using System.Threading.Tasks;
using Cli;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Workspace;

public static class WebApplicationExtensions
{
    public static async Task RunRingAsync(this WebApplication app)
    {
        var opts = app.Services.GetRequiredService<BaseOptions>();
        switch (opts)
        {
            case CloneOptions c:
                await app.Services.GetRequiredService<ICloneMaker>().CloneWorkspaceRepos(c.WorkspacePath!, c.OutputDir);
                break;
            case ConfigDump:
                {
                    var debugView = ((IConfigurationRoot)app.Services.GetRequiredService<IConfiguration>()).GetDebugView();
                    Console.WriteLine(debugView);
                    break;
                }
            case HeadlessOptions:
            case ConsoleOptions:
                await app.RunAsync();
                break;
            default:
                throw new InvalidOperationException("CLI is misconfigured");
        }
    }
}
