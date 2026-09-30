namespace TopDownDoom.Design;

public readonly record struct MonsterSpawn(
    MonsterRole Role,
    float WorldX,
    float WorldZ,
    string ClosetTag = "");
