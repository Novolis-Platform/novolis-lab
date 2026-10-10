using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class WinDto
{
    public bool Won { get; set; }
    public bool Lost { get; set; }
    public string Message { get; set; } = "";
}
