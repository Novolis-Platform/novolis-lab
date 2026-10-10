using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class LinkDto
{
    public Guid From { get; set; }
    public Guid To { get; set; }
}
