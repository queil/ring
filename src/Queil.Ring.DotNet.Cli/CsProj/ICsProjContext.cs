namespace Queil.Ring.DotNet.Cli.CsProj;

public interface ICsProjContext
{
    string? CsProjPath { get; }
    string WorkingDir { get; }
    string? TargetFramework { get; }
    string? TargetRuntime { get; }
    string EntryAssemblyPath { get; }
}
