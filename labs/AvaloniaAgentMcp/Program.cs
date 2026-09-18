using AvaloniaAgentMcp;

if (args.Contains("--mcp", StringComparer.OrdinalIgnoreCase) || args.Length == 0)
{
    await AvaloniaAgentMcpHost.RunStdioAsync(args);
    return;
}

Console.Error.WriteLine("AvaloniaAgentMcp: pass --mcp (default) to run the stdio MCP server.");
Environment.Exit(1);
