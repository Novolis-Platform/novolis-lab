namespace TopDownDoom.Design;

public sealed record EncounterReward(
    int HealthBonus,
    int ArmorBonus,
    int AmmoBonus,
    bool IsSecret);
