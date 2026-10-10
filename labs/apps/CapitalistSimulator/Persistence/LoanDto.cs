using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class LoanDto
{
    public decimal Principal { get; set; }
    public decimal MonthlyRate { get; set; }
}
