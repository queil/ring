namespace Queil.Ring.DotNet.Cli.CsProj.LaunchSettings;

using System;

public class iisExpress
{
    public required Uri applicationUrl { get; init; }
    public required int sslPort { get; init; }
}
