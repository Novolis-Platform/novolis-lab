namespace CapitalistSimulator.Sim;

internal enum FirmKind
{
    Retail,
    Factory,
    Farm,
    Extract,
    Rd,
    Hq,
}

internal enum RetailFamily
{
    Department,
    Supermarket,
    Convenience,
    SpecialtyGoods,
    Auto,
    Electronics,
}

internal enum UnitKind
{
    Purchasing,
    Manufacturing,
    Sales,
    Inventory,
    Advertising,
    Rd,
    Extract,
}

internal enum ExtractKind
{
    None,
    Crop,
    Livestock,
    Mine,
    Forest,
    Oil,
}

internal enum BrandStrategy
{
    Corporate,
    Range,
    Unique,
}

internal enum TileKind
{
    Buildable,
    Road,
    Seaport,
    Bank,
    StockExchange,
    Blocked,
}

internal enum EconomicClimate
{
    Boom,
    Growth,
    Stable,
    Slowdown,
    Recession,
    Panic,
}

internal enum ProductClass
{
    Apparel,
    Auto,
    Beverage,
    Computer,
    Drug,
    Electronics,
    Food,
    Furniture,
    Jewelry,
    Raw,
    Semi,
    Sport,
    Toy,
    Wine,
    Livestock,
}

internal enum ScenarioId
{
    Sandbox,
    RetailProfit,
    WineDominance,
}
