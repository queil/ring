using System;

namespace Queil.Ring.DotNet.Cli.Abstractions.Context;

public interface ITrackUri
{
    Uri Uri { get; set; }
}