namespace CapitalistSimulator.Sim;

internal abstract record PlayerCommand;

internal sealed record NewGameCommand(
    ScenarioId Scenario,
    decimal StartingCash,
    int AiCount,
    double AiAggressiveness,
    int Seed) : PlayerCommand;

internal sealed record SetSpeedCommand(int Speed) : PlayerCommand;
internal sealed record SetPausedCommand(bool Paused) : PlayerCommand;
internal sealed record AdvanceDaysCommand(int Days) : PlayerCommand;
internal sealed record SelectCityCommand(string CityName) : PlayerCommand;

internal sealed record BuildFirmCommand(
    string CityName,
    string FirmTypeId,
    int TileX,
    int TileY,
    string? Name = null) : PlayerCommand;

internal sealed record DemolishFirmCommand(FirmId FirmId) : PlayerCommand;

internal sealed record PlaceUnitCommand(
    FirmId FirmId,
    UnitKind Kind,
    int X,
    int Y) : PlayerCommand;

internal sealed record RemoveUnitCommand(FirmId FirmId, UnitId UnitId) : PlayerCommand;

internal sealed record SetLinkCommand(FirmId FirmId, UnitId From, UnitId To) : PlayerCommand;
internal sealed record ClearLinksCommand(FirmId FirmId) : PlayerCommand;
internal sealed record AutoLinkCommand(FirmId FirmId) : PlayerCommand;

internal sealed record SetTrainingCommand(FirmId FirmId, UnitId UnitId, double Training) : PlayerCommand;

internal sealed record ConfigurePurchasingCommand(
    FirmId FirmId,
    UnitId UnitId,
    string ProductId,
    decimal QtyTarget,
    bool FromSeaport,
    FirmId? FromFirm,
    bool PrivateLabel) : PlayerCommand;

internal sealed record ConfigureManufacturingCommand(
    FirmId FirmId,
    UnitId UnitId,
    string RecipeOutputId,
    decimal ProductionRate) : PlayerCommand;

internal sealed record ConfigureSalesCommand(
    FirmId FirmId,
    UnitId UnitId,
    string ProductId,
    decimal Price) : PlayerCommand;

internal sealed record ConfigureAdvertisingCommand(
    FirmId FirmId,
    UnitId UnitId,
    string? ProductId,
    string? ProductClass,
    decimal Budget) : PlayerCommand;

internal sealed record ConfigureExtractCommand(
    FirmId FirmId,
    UnitId UnitId,
    ExtractKind Kind,
    string ProductId,
    decimal Yield) : PlayerCommand;

internal sealed record StartRdCommand(FirmId FirmId, UnitId UnitId, string ProductId, int Months) : PlayerCommand;
internal sealed record CancelRdCommand(FirmId FirmId, UnitId UnitId) : PlayerCommand;
internal sealed record SetAutoApplyRdCommand(FirmId FirmId, bool Auto) : PlayerCommand;

internal sealed record SetBrandStrategyCommand(BrandStrategy Strategy) : PlayerCommand;

internal sealed record BorrowCommand(decimal Amount) : PlayerCommand;
internal sealed record RepayCommand(decimal Amount) : PlayerCommand;

internal sealed record BuySharesCommand(CorpId Issuer, decimal Shares) : PlayerCommand;
internal sealed record SellSharesCommand(CorpId Issuer, decimal Shares) : PlayerCommand;
internal sealed record IssueSharesCommand(decimal Shares, decimal Price) : PlayerCommand;
internal sealed record SetDividendCommand(decimal PerShare) : PlayerCommand;

internal sealed record SetHqFinanceAutoCommand(bool On) : PlayerCommand;
internal sealed record SetHqMarketingAutoCommand(bool On) : PlayerCommand;
internal sealed record SetHqImportPreferInternalCommand(bool On) : PlayerCommand;
internal sealed record SetHqRdAutoCommand(bool On) : PlayerCommand;

internal sealed record RetireCommand() : PlayerCommand;
internal sealed record AbsorbCorpCommand(CorpId Target) : PlayerCommand;

internal sealed record CommandResult(bool Ok, string Message)
{
    public static CommandResult Success(string message = "OK") => new(true, message);
    public static CommandResult Fail(string message) => new(false, message);
}
