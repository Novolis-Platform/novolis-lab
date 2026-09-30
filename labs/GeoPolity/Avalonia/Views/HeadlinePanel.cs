using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GeoPolity.Session;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Agent.Protocol;
using Novolis.Avalonia.Briefing;

namespace GeoPolity.AvaloniaUi.Views;

internal sealed class HeadlinePanel : UserControl
{
    private readonly FeedPanel _feed = new() { MinHeight = 140 };

    public HeadlinePanel()
    {
        Background = new SolidColorBrush(Color.Parse("#0e2030"));
        Padding = new Thickness(8);
        var root = new DockPanel();
        var title = new TextBlock
        {
            Text = "HEADLINES",
            Foreground = new SolidColorBrush(Color.Parse("#c47b3a")),
            FontWeight = FontWeight.Bold,
            FontSize = 13,
            Margin = new Thickness(4, 0, 0, 6),
            [DockPanel.DockProperty] = Dock.Top,
        };
        root.Children.Add(title);
        root.Children.Add(_feed);
        Content = root;
        AgentProperties.SetId(_feed, "geopolity.headlines");
        AgentProperties.SetRole(_feed, AgentRoleNames.ListBox);
    }

    public void Bind(GeoSession session)
    {
        var lines = session.Headlines.Lines
            .Select(h => new FeedLine(h.Voice, h.Text, $"D{h.Day} {h.Tag}"))
            .ToList();
        if (lines.Count == 0)
        {
            lines.Add(new FeedLine("status", "quiet — waiting for wars, peaces, orgs…"));
        }

        _feed.SetLines(lines);
    }
}
