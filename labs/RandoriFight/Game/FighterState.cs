namespace RandoriFight.Game;

internal enum FighterState
{
    /// <summary>Chūdan-no-kamae — middle guard.</summary>
    Idle,
    Walk,
    /// <summary>Men — vertical cut to the head line.</summary>
    Men,
    /// <summary>Kesa-giri — diagonal cut.</summary>
    Kesa,
    /// <summary>Tsuki — thrust on the center line.</summary>
    Thrust,
    /// <summary>Do — horizontal torso cut.</summary>
    Do,
    /// <summary>Kote — wrist / gedan cut.</summary>
    Kote,
    /// <summary>Kirioroshi — descending cut.</summary>
    Kirioroshi,
    /// <summary>Uke — receiving with blade in line.</summary>
    Parry,
    HitStun,
    Ko,
}
