namespace Queil.Ring.DotNet.Cli.Tools;

using System.Threading.Tasks;

public readonly struct ExecutionInfo(int pid, int? exitCode, string output, Task<ExecutionInfo>? task)
{
    public int Pid { get; } = pid;
    public int? ExitCode { get; } = exitCode;
    public string Output { get; } = output;
    public bool IsSuccess => ExitCode == 0;
    public Task<ExecutionInfo>? Task { get; } = task;
    public static readonly ExecutionInfo Empty = new(0, 0, string.Empty, null);
}
