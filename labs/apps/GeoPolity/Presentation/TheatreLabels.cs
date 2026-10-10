using Novolis.Geopolitics.Core;

namespace GeoPolity.Presentation;

/// <summary>UI-only labels derived from Core science fields (not stored on Core).</summary>
public static class TheatreLabels
{
    public static string HabitatKind(Province province)
    {
        if (province.Coastal)
        {
            return "Port";
        }

        var w = province.ResourceWeights;
        var best = ResourceKind.Food;
        var bestVal = -1.0;
        foreach (var k in ResourceKinds.All)
        {
            var v = w[k];
            if (v > bestVal)
            {
                bestVal = v;
                best = k;
            }
        }

        return best switch
        {
            ResourceKind.Food => "Agri",
            ResourceKind.Energy => "Energy",
            ResourceKind.Materials => "Mine",
            ResourceKind.Goods => "Industry",
            ResourceKind.MilitaryGoods => "Arsenal",
            ResourceKind.Rare => "Rare",
            _ => "Habitat",
        };
    }

    public static string HabitatTag(Province province)
    {
        var label = HabitatKind(province);
        return label.Length <= 3 ? label : label[..3];
    }

    public static string OrgKind(SupranationalKind kind) => kind switch
    {
        SupranationalKind.Forum => "Forum",
        SupranationalKind.DefenceAlliance => "Defence",
        SupranationalKind.FreeTradeArea => "FTA",
        SupranationalKind.CustomsUnion => "Customs",
        SupranationalKind.ResearchForum => "Research",
        SupranationalKind.PoliticalUnion => "Union",
        _ => kind.ToString(),
    };
}
