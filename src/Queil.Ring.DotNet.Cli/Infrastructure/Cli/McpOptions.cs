namespace Queil.Ring.DotNet.Cli.Infrastructure.Cli;

using CommandLine;

[Verb("mcp", HelpText = "Starts ring! as an MCP server using stdio transport")]
public class McpOptions : BaseOptions
{
    [Option('w', "workspace", Required = false, HelpText = "Workspace path to auto-load and start")]
    public string? WorkspacePath { get; set; }
}
