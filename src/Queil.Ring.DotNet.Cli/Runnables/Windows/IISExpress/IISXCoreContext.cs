using System;
using Queil.Ring.Configuration.Interfaces;
using Queil.Ring.DotNet.Cli.CsProj;
using Queil.Ring.DotNet.Cli.Abstractions.Context;
using Queil.Ring.DotNet.Cli.Runnables.Dotnet;

namespace Queil.Ring.DotNet.Cli.Runnables.Windows.IISExpress;

public class IISXCoreContext : DotnetContext, ITrackUri
{
    public string TempAppHostConfigPath { get; set; }
    public Uri Uri { get; set; }
    public static IISXCoreContext Create<C>(C config, Func<IFromGit, string> resolveFullClonePath) where C : IUseCsProjFile
    {
        var ctx = Create<IISXCoreContext, C>(config, resolveFullClonePath);
        ctx.Uri = config.GetIISUrl();
        return ctx;
    }
}
