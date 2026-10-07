namespace AssetStudioLab;

/// <summary>Canonical sample programs for the lab.</summary>
public static class VisualSamples
{
    public const string NavalSteel = """
        material NavalSteel {
            color #747a7d
            roughness 0.72
            roughness *= noise(scale: 18mm, amount: 0.05)
            normal += brushed(direction: y, strength: 0.03)
            layer edgeWear(amount: 0.08)
            layer cavityGrime(amount: 0.12)
        }
        """;

    public const string ShipSteel = """
        material ShipSteel(
            color = #747a7d,
            wear = 0.08,
            grime = 0.12)
        {
            color color
            roughness 0.72
            layer edgeWear(amount: wear)
            layer cavityGrime(amount: grime)
        }
        """;

    public const string RedBolt = """
        effect RedBolt {
            geometry capsule(length: 35cm, radius: 12mm)
            surface {
                emission radial(core: white * 180, edge: red * 65)
            }
            light point(color: red, intensity: 2200, radius: 4m)
            trail {
                rate 160/s
                lifetime 90ms
                velocity inherit * 0.1
                emission red * fade(age)
            }
        }
        """;

    public const string MarineFlashlight = """
        light MarineFlashlight {
            spot
            temperature 5100K
            intensity 1700lm
            range 28m
            cone 44deg
            penumbra 9deg
            shadows hard
        }
        """;

    public const string ShipFog = """
        volume ShipFog {
            density 0.025
            scattering 0.82
            density *= noise(scale: 1.8m, amount: 0.35)
        }
        """;

    public const string GeometryPanel = """
        geometry Bulkhead {
            Box(2m, 3m, 0.2m)
            Bevel(4mm)
            Panelize(1.2m, 0.8m, inset: 12mm)
            Material NavalSteel
        }
        """;
}
