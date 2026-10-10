using MauiAgentMcp;

if (args.Contains("--mcp", StringComparer.OrdinalIgnoreCase) || args.Length == 0)
{
    await MauiAgentMcpHost.RunStdioAsync(args);
    return;
}

Console.Error.WriteLine("MauiAgentMcp: pass --mcp (default) to run the stdio MCP server.");
Environment.Exit(1);
