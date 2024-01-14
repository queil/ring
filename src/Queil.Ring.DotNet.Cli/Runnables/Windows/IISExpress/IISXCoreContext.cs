namespace Queil.Ring.DotNet.Cli.Runnables.Windows.IISExpress;

using System;
using Abstractions.Context;
using Configuration;
using CsProj;
using Dotnet;

public class IISXCoreContext : DotnetContext, ITrackUri
{
    public string TempAppHostConfigPath { get; set; }
    public Uri Uri { get; set; }

    public static IISXCoreContext Create<C>(C config, Func<IFromGit, string> resolveFullClonePath)
        where C : IUseCsProjFile
    {
        var ctx = Create<IISXCoreContext, C>(config, resolveFullClonePath);
        ctx.Uri = config.GetIISUrl();
        return ctx;
    }
}
