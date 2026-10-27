namespace CalypsoCad.Generation.Fabrication;

internal static class CalypsoMaterialMap
{
    public static float DensityKgM3(string category) =>
        category switch
        {
            "outfit" or "install" => 2700f,
            "partition" => 8000f,
            _ => 8000f,
        };

    public static string Designation(string category) =>
        category switch
        {
            "outfit" or "install" => "Aluminum 6061-T6",
            _ => "AISI 316L",
        };
}
