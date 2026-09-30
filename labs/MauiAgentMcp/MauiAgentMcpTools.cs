using System.ComponentModel;
using ModelContextProtocol.Server;
using Novolis.Maui.Agent.Protocol.Dto;

namespace MauiAgentMcp;

[McpServerToolType]
public static class MauiAgentMcpTools
{
    [McpServerTool]
    [Description("List known MAUI agent host endpoints (temp marker + known pipes). Use before ui_connect when multiple apps run.")]
    public static string UiHosts() =>
        MauiAgentRuntime.ToJson(new
        {
            hosts = MauiAgentRuntime.DiscoverHosts(),
            activeOverride = MauiAgentRuntime.EndpointOverride,
        });

    [McpServerTool]
    [Description("Connect to a named-pipe / socket endpoint (e.g. novolis-maui-agent). Empty clears override and uses host marker / default.")]
    public static async Task<string> UiConnect(
        [Description("Pipe/socket name. Omit or empty to clear override and auto-discover.")]
        string? endpoint = null,
        CancellationToken cancellationToken = default)
    {
        await MauiAgentRuntime.SetEndpointAsync(endpoint).ConfigureAwait(false);
        var response = await MauiAgentRuntime.WithClientAsync(
            client => client.HelloAsync(cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return MauiAgentRuntime.ToJson(new
        {
            endpoint = MauiAgentRuntime.EndpointOverride ?? "(auto)",
            response,
        });
    }

    [McpServerTool]
    [Description("Drop the cached MAUI IPC client and handshake again (use after the host app restarts).")]
    public static async Task<string> UiReconnect(CancellationToken cancellationToken = default)
    {
        await MauiAgentRuntime.ForceReconnectAsync().ConfigureAwait(false);
        var response = await MauiAgentRuntime.WithClientAsync(
            client => client.HelloAsync(cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return MauiAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Handshake with the MAUI agent host: protocol version, app title, process id.")]
    public static async Task<string> UiHello(CancellationToken cancellationToken = default)
    {
        var response = await MauiAgentRuntime.WithClientAsync(
            client => client.HelloAsync(cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return MauiAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Compact multi-get: read text/enabled/visible for many AgentIds in one round-trip (prefer over full ui_tree).")]
    public static async Task<string> UiGet(
        [Description("AgentIds to read, e.g. PdfStatus,PdfPageLabel,PdfDocumentTitle")]
        string[] controlIds,
        CancellationToken cancellationToken = default)
    {
        var response = await MauiAgentRuntime.WithClientAsync(
            client => client.GetAsync(controlIds ?? [], cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return MauiAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("List items in a CollectionView (index, text, selected). Use for the PDF page rail without selecting.")]
    public static async Task<string> UiItems(
        [Description("AgentId of the CollectionView, e.g. PdfPageRail")]
        string controlId,
        CancellationToken cancellationToken = default)
    {
        var response = await MauiAgentRuntime.WithClientAsync(
            client => client.ItemsAsync(controlId, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return MauiAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Client-side poll of ui.get until textContains matches (or enabled). Prefer this over ui_wait while the UI is busy.")]
    public static async Task<string> UiPoll(
        [Description("Control AgentId to watch.")]
        string controlId,
        [Description("Optional substring the control text must contain.")]
        string? textContains = null,
        [Description("Optional required IsEnabled.")]
        bool? enabled = null,
        [Description("Timeout in milliseconds (default 60000).")]
        int timeoutMs = 60000,
        [Description("Poll interval in milliseconds (default 400).")]
        int intervalMs = 400,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
        UiGetResponseDto? last = null;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            last = await MauiAgentRuntime.WithClientAsync(
                client => client.GetAsync([controlId], cancellationToken).AsTask(),
                cancellationToken).ConfigureAwait(false);

            var state = last.Controls.FirstOrDefault();
            if (state is { Found: true })
            {
                var enabledOk = enabled is null || state.IsEnabled == enabled;
                var textOk = string.IsNullOrEmpty(textContains)
                             || (state.Text?.Contains(textContains, StringComparison.OrdinalIgnoreCase) ?? false);
                if (enabledOk && textOk)
                {
                    return MauiAgentRuntime.ToJson(new
                    {
                        success = true,
                        timedOut = false,
                        control = state,
                        appTitle = last.AppTitle,
                        processId = last.ProcessId,
                    });
                }
            }

            await Task.Delay(Math.Max(50, intervalMs), cancellationToken).ConfigureAwait(false);
        }

        return MauiAgentRuntime.ToJson(new
        {
            success = false,
            timedOut = true,
            control = last?.Controls.FirstOrDefault(),
            appTitle = last?.AppTitle,
            processId = last?.ProcessId,
            error = $"Timed out waiting for '{controlId}'.",
        });
    }

    [McpServerTool]
    [Description("Dump the MAUI interactive control tree (ids, roles, bounds, text, enabled/focused).")]
    public static async Task<string> UiTree(
        [Description("When true (default), only interactive controls and AgentId-tagged controls.")]
        bool interactiveOnly = true,
        CancellationToken cancellationToken = default)
    {
        var response = await MauiAgentRuntime.WithClientAsync(
            client => client.TreeAsync(interactiveOnly, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return MauiAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Capture a PNG screenshot of the MAUI page (or a control by id). Returns JSON with file path under %TEMP%/novolis-maui-agent/.")]
    public static async Task<string> UiScreenshot(
        [Description("Optional AgentId / AutomationId. Null = whole page.")]
        string? controlId = null,
        [Description("Optional max width in pixels; height scales proportionally.")]
        int? maxWidth = null,
        CancellationToken cancellationToken = default)
    {
        var response = await MauiAgentRuntime.WithClientAsync(
            client => client.ScreenshotAsync(controlId, maxWidth, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);

        if (!response.Success)
            return MauiAgentRuntime.ToJson(response);

        var path = MauiAgentRuntime.WriteScreenshot(response);
        return MauiAgentRuntime.ToJson(new
        {
            response.RequestId,
            response.Success,
            response.Error,
            path,
            response.Width,
            response.Height,
        });
    }

    [McpServerTool]
    [Description("Click a MAUI control by AgentId, or at page coordinates (x, y).")]
    public static async Task<string> UiClick(
        [Description("Stable AgentId / AutomationId, e.g. PdfFit.")]
        string? controlId = null,
        [Description("Page X when controlId is omitted.")]
        double? x = null,
        [Description("Page Y when controlId is omitted.")]
        double? y = null,
        [Description("Mouse button: left (default), right, or middle.")]
        string? button = null,
        [Description("Click count; 2 for double-click.")]
        int clickCount = 1,
        CancellationToken cancellationToken = default)
    {
        var response = await MauiAgentRuntime.WithClientAsync(
            client => client.ClickAsync(controlId, x, y, button, clickCount, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return MauiAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Type text into a focused or id-targeted Entry/Editor/SearchBar, and/or send special keys (Enter, Tab, Escape).")]
    public static async Task<string> UiType(
        [Description("Optional control AgentId to focus first.")]
        string? controlId = null,
        [Description("Text to append into an Entry (or replace when clear=true).")]
        string? text = null,
        [Description("Special keys, e.g. Enter, Tab, Escape.")]
        string[]? keys = null,
        [Description("When true, replace Entry text instead of appending.")]
        bool clear = false,
        CancellationToken cancellationToken = default)
    {
        var response = await MauiAgentRuntime.WithClientAsync(
            client => client.TypeAsync(controlId, text, keys, clear, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return MauiAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Select an item in a CollectionView by zero-based index or item text substring.")]
    public static async Task<string> UiSelect(
        [Description("Stable AgentId of the CollectionView.")]
        string controlId,
        [Description("Zero-based index. Prefer this when known.")]
        int? index = null,
        [Description("Substring match against item text (case-insensitive).")]
        string? itemText = null,
        CancellationToken cancellationToken = default)
    {
        var response = await MauiAgentRuntime.WithClientAsync(
            client => client.SelectAsync(controlId, index, itemText, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return MauiAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Focus a MAUI control by AgentId.")]
    public static async Task<string> UiFocus(
        [Description("Stable AgentId to focus.")]
        string controlId,
        CancellationToken cancellationToken = default)
    {
        var response = await MauiAgentRuntime.WithClientAsync(
            client => client.FocusAsync(controlId, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return MauiAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Scroll the nearest ScrollView by delta.")]
    public static async Task<string> UiScroll(
        [Description("Optional AgentId; omit to scroll the page.")]
        string? controlId = null,
        [Description("Horizontal scroll delta.")]
        double? deltaX = null,
        [Description("Vertical scroll delta.")]
        double? deltaY = null,
        [Description("When true, bring the control into view.")]
        bool bringIntoView = false,
        CancellationToken cancellationToken = default)
    {
        var response = await MauiAgentRuntime.WithClientAsync(
            client => client.ScrollAsync(controlId, deltaX, deltaY, bringIntoView, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return MauiAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Host-side wait until a control id matches. Prefer ui_poll when the app is busy.")]
    public static async Task<string> UiWait(
        [Description("Control AgentId to wait for.")]
        string controlId,
        [Description("Optional required IsEnabled value.")]
        bool? enabled = null,
        [Description("Optional substring that control text must contain.")]
        string? textContains = null,
        [Description("Timeout in milliseconds (default 5000).")]
        int timeoutMs = 5000,
        CancellationToken cancellationToken = default)
    {
        var response = await MauiAgentRuntime.WithClientAsync(
            client => client.WaitAsync(controlId, enabled, textContains, timeoutMs, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return MauiAgentRuntime.ToJson(response);
    }
}
