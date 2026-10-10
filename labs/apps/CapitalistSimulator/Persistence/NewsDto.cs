using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class NewsDto
{
    public int Day { get; set; }
    public string Text { get; set; } = "";
}
