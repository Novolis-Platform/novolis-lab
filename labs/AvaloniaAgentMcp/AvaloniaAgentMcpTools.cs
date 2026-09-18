using System.ComponentModel;
using ModelContextProtocol.Server;
using Novolis.Avalonia.Agent.Protocol.Dto;

namespace AvaloniaAgentMcp;

[McpServerToolType]
public static class AvaloniaAgentMcpTools
{
    [McpServerTool]
    [Description("List known Avalonia agent host endpoints (temp marker + known pipes). Use before ui_connect when multiple apps run.")]
    public static string UiHosts() =>
        AvaloniaAgentRuntime.ToJson(new
        {
            hosts = AvaloniaAgentRuntime.DiscoverHosts(),
            activeOverride = AvaloniaAgentRuntime.EndpointOverride
        });

    [McpServerTool]
    [Description("Connect to a named-pipe / socket endpoint (e.g. novolis-avalonia-agent-sins). Empty clears override and uses host marker / default.")]
    public static async Task<string> UiConnect(
        [Description("Pipe/socket name. Omit or empty to clear override and auto-discover.")]
        string? endpoint = null,
        CancellationToken cancellationToken = default)
    {
        await AvaloniaAgentRuntime.SetEndpointAsync(endpoint).ConfigureAwait(false);
        var response = await AvaloniaAgentRuntime.WithClientAsync(
            c => c.HelloAsync(cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(new
        {
            endpoint = AvaloniaAgentRuntime.EndpointOverride ?? "(auto)",
            response
        });
    }

    [McpServerTool]
    [Description("Drop the cached Avalonia IPC client and handshake again (use after the host app restarts).")]
    public static async Task<string> UiReconnect(CancellationToken cancellationToken = default)
    {
        await AvaloniaAgentRuntime.ForceReconnectAsync().ConfigureAwait(false);
        var response = await AvaloniaAgentRuntime.WithClientAsync(
            c => c.HelloAsync(cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Handshake with the Avalonia agent host: protocol version, app title, process id.")]
    public static async Task<string> UiHello(CancellationToken cancellationToken = default)
    {
        var response = await AvaloniaAgentRuntime.WithClientAsync(
            c => c.HelloAsync(cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Compact multi-get: read text/enabled/visible for many AgentIds in one round-trip (prefer over full ui_tree for bridge state).")]
    public static async Task<string> UiGet(
        [Description("AgentIds to read, e.g. calypso.voyage,calypso.survival,calypso.continue")]
        string[] controlIds,
        CancellationToken cancellationToken = default)
    {
        var response = await AvaloniaAgentRuntime.WithClientAsync(
            c => c.GetAsync(controlIds ?? Array.Empty<string>(), cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("List items in a ListBox, ComboBox, or TabControl (index, text, selected). Use for spot boards / tabs without selecting.")]
    public static async Task<string> UiItems(
        [Description("AgentId of the list/combo/tabs control, e.g. calypso.spot")]
        string controlId,
        CancellationToken cancellationToken = default)
    {
        var response = await AvaloniaAgentRuntime.WithClientAsync(
            c => c.ItemsAsync(controlId, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Client-side poll of ui.get until textContains matches (or enabled). Prefer this over ui_wait while the UI sim is busy — does not block the Avalonia UI thread.")]
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
            last = await AvaloniaAgentRuntime.WithClientAsync(
                c => c.GetAsync(new[] { controlId }, cancellationToken).AsTask(),
                cancellationToken).ConfigureAwait(false);

            var state = last.Controls.FirstOrDefault();
            if (state is { Found: true })
            {
                var enabledOk = enabled is null || state.IsEnabled == enabled;
                var textOk = string.IsNullOrEmpty(textContains)
                             || (state.Text?.Contains(textContains, StringComparison.OrdinalIgnoreCase) ?? false);
                if (enabledOk && textOk)
                {
                    return AvaloniaAgentRuntime.ToJson(new
                    {
                        success = true,
                        timedOut = false,
                        control = state,
                        appTitle = last.AppTitle,
                        processId = last.ProcessId
                    });
                }
            }

            await Task.Delay(Math.Max(50, intervalMs), cancellationToken).ConfigureAwait(false);
        }

        return AvaloniaAgentRuntime.ToJson(new
        {
            success = false,
            timedOut = true,
            control = last?.Controls.FirstOrDefault(),
            appTitle = last?.AppTitle,
            processId = last?.ProcessId,
            error = $"Timed out waiting for '{controlId}'."
        });
    }

    [McpServerTool]
    [Description("Dump the Avalonia interactive control tree (ids, roles, bounds, text, enabled/focused). List AgentIds also emit item rows.")]
    public static async Task<string> UiTree(
        [Description("When true (default), only interactive controls and AgentId-tagged controls.")]
        bool interactiveOnly = true,
        CancellationToken cancellationToken = default)
    {
        var response = await AvaloniaAgentRuntime.WithClientAsync(
            c => c.TreeAsync(interactiveOnly, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Capture a PNG screenshot of the Avalonia window (or a control by id). Returns JSON with file path under %TEMP%/novolis-avalonia-agent/.")]
    public static async Task<string> UiScreenshot(
        [Description("Optional AgentId / control id. Null = whole window.")]
        string? controlId = null,
        [Description("Optional max width in pixels; height scales proportionally.")]
        int? maxWidth = null,
        CancellationToken cancellationToken = default)
    {
        var response = await AvaloniaAgentRuntime.WithClientAsync(
            c => c.ScreenshotAsync(controlId, maxWidth, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);

        if (!response.Success)
            return AvaloniaAgentRuntime.ToJson(response);

        var path = AvaloniaAgentRuntime.WriteScreenshot(response);
        return AvaloniaAgentRuntime.ToJson(new
        {
            response.RequestId,
            response.Success,
            response.Error,
            path,
            response.Width,
            response.Height
        });
    }

    [McpServerTool]
    [Description("Click an Avalonia control by AgentId, or at window coordinates (x, y). Supports button=left|right|middle and clickCount for double-click.")]
    public static async Task<string> UiClick(
        [Description("Stable AgentId, e.g. lab.recovery.")]
        string? controlId = null,
        [Description("Window X when controlId is omitted.")]
        double? x = null,
        [Description("Window Y when controlId is omitted.")]
        double? y = null,
        [Description("Mouse button: left (default), right, or middle.")]
        string? button = null,
        [Description("Click count; 2 for double-click.")]
        int clickCount = 1,
        CancellationToken cancellationToken = default)
    {
        var response = await AvaloniaAgentRuntime.WithClientAsync(
            c => c.ClickAsync(controlId, x, y, button, clickCount, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Type text into a focused or id-targeted control, and/or send special keys (Enter, Tab, Escape).")]
    public static async Task<string> UiType(
        [Description("Optional control AgentId to focus first.")]
        string? controlId = null,
        [Description("Text to append into a TextBox (or replace when clear=true).")]
        string? text = null,
        [Description("Special keys, e.g. Enter, Tab, Escape.")]
        string[]? keys = null,
        [Description("When true, replace TextBox text instead of appending.")]
        bool clear = false,
        CancellationToken cancellationToken = default)
    {
        var response = await AvaloniaAgentRuntime.WithClientAsync(
            c => c.TypeAsync(controlId, text, keys, clear, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Select an item in a ListBox, ComboBox, or TabControl by zero-based index or item/header text substring.")]
    public static async Task<string> UiSelect(
        [Description("Stable AgentId of the list/combo/tabs control.")]
        string controlId,
        [Description("Zero-based index. Prefer this when known.")]
        int? index = null,
        [Description("Substring match against item text or tab header (case-insensitive).")]
        string? itemText = null,
        CancellationToken cancellationToken = default)
    {
        var response = await AvaloniaAgentRuntime.WithClientAsync(
            c => c.SelectAsync(controlId, index, itemText, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Focus an Avalonia control by AgentId.")]
    public static async Task<string> UiFocus(
        [Description("Stable AgentId to focus.")]
        string controlId,
        CancellationToken cancellationToken = default)
    {
        var response = await AvaloniaAgentRuntime.WithClientAsync(
            c => c.FocusAsync(controlId, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Scroll the nearest ScrollViewer by delta, or bring a control into view.")]
    public static async Task<string> UiScroll(
        [Description("Optional AgentId; omit to scroll the main window viewer.")]
        string? controlId = null,
        [Description("Horizontal scroll delta in DIPs.")]
        double? deltaX = null,
        [Description("Vertical scroll delta in DIPs.")]
        double? deltaY = null,
        [Description("When true, call BringIntoView on the control instead of applying deltas.")]
        bool bringIntoView = false,
        CancellationToken cancellationToken = default)
    {
        var response = await AvaloniaAgentRuntime.WithClientAsync(
            c => c.ScrollAsync(controlId, deltaX, deltaY, bringIntoView, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }

    [McpServerTool]
    [Description("Host-side wait until a control id matches (blocks Avalonia UI thread). Prefer ui_poll when the app is simulating.")]
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
        var response = await AvaloniaAgentRuntime.WithClientAsync(
            c => c.WaitAsync(controlId, enabled, textContains, timeoutMs, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
        return AvaloniaAgentRuntime.ToJson(response);
    }
}
