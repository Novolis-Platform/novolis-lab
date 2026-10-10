using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class SaveDto
{
    public ScenarioId Scenario { get; set; }
    public int Day { get; set; }
    public int Seed { get; set; }
    public int Speed { get; set; }
    public bool Paused { get; set; }
    public string? SelectedCityName { get; set; }
    public decimal ScenarioTargetProfit { get; set; }
    public int ScenarioMaxDays { get; set; }
    public List<CorpDto> Corporations { get; set; } = [];
    public List<CityDto> Cities { get; set; } = [];
    public List<FirmDto> Firms { get; set; } = [];
    public List<HoldingDto> Holdings { get; set; } = [];
    public List<NewsDto> News { get; set; } = [];
    public WinDto Win { get; set; } = new();
}
