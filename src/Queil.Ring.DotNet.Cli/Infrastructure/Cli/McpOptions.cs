namespace Queil.Ring.DotNet.Cli.Infrastructure.Cli;

using CommandLine;

[Verb("mcp", HelpText = "Runs ring! as an MCP (Model Context Protocol) server - controllable by Claude")]
public class McpOptions : BaseOptions
{
    [Option('w', "workspace", Required = false, HelpText = "Workspace path to auto-load on start")]
    public string? WorkspacePath { get; set; }
}
