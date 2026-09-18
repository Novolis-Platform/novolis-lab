namespace MusicMakerLab;

internal enum FreeMediaKind
{
    Midi,
    Audio,
}

/// <summary>One free/online media entry with license metadata.</summary>
internal sealed record FreeMediaEntry(
    string Id,
    string Title,
    string ArtistOrSource,
    FreeMediaKind Kind,
    string DownloadUrl,
    string License,
    string LicenseUrl,
    string? FileName = null)
{
    public string LocalFileName =>
        FileName ?? (Kind == FreeMediaKind.Midi ? $"{Id}.mid" : $"{Id}.mp3");
}

/// <summary>
/// Curated free online MIDI + SFX for Music Maker Lab.
/// MIDI: Mutopia Project (public domain / open). Audio: Mixkit (Mixkit License — free commercial use).
/// </summary>
internal static class FreeMediaCatalog
{
    public const string MutopiaHome = "https://www.mutopiaproject.org/";
    public const string MixkitLicense = "https://mixkit.co/license/#sfxFree";

    public static IReadOnlyList<FreeMediaEntry> All { get; } =
    [
        // —— Public-domain / Mutopia MIDI ——
        Midi("bach-inv-01", "Bach · Invention 1 (BWV 772)", "J. S. Bach / Mutopia",
            "https://www.mutopiaproject.org/ftp/BachJS/BWV772/bach-invention-01/bach-invention-01.mid"),
        Midi("bach-inv-02", "Bach · Invention 2 (BWV 773)", "J. S. Bach / Mutopia",
            "https://www.mutopiaproject.org/ftp/BachJS/BWV773/bach-invention-02/bach-invention-02.mid"),
        Midi("bach-inv-03", "Bach · Invention 3 (BWV 774)", "J. S. Bach / Mutopia",
            "https://www.mutopiaproject.org/ftp/BachJS/BWV774/bach-invention-03/bach-invention-03.mid"),
        Midi("bach-inv-13", "Bach · Invention 13 (BWV 784)", "J. S. Bach / Mutopia",
            "https://www.mutopiaproject.org/ftp/BachJS/BWV784/bach-invention-13/bach-invention-13.mid"),
        Midi("bach-wtk1-p1", "Bach · WTC I Prelude 1 (BWV 846)", "J. S. Bach / Mutopia",
            "https://www.mutopiaproject.org/ftp/BachJS/BWV846/wtk1-prelude1/wtk1-prelude1.mid"),
        Midi("beethoven-pathetique-1", "Beethoven · Pathétique I (Op. 13)", "L. van Beethoven / Mutopia",
            "https://www.mutopiaproject.org/ftp/BeethovenLv/O13/pathetique-1/pathetique-1.mid"),
        Midi("mozart-sym25-1", "Mozart · Symphony 25 I (K.183)", "W. A. Mozart / Mutopia",
            "https://www.mutopiaproject.org/ftp/MozartWA/KV183/Symphony25_1/Symphony25_1.mid"),
        Midi("mozart-menuet-k2", "Mozart · Menuet (K.2)", "W. A. Mozart / Mutopia",
            "https://www.mutopiaproject.org/ftp/MozartWA/KV2/menuet_k2/menuet_k2.mid"),
        Midi("mozart-ave-verum", "Mozart · Ave verum corpus", "W. A. Mozart / Mutopia",
            "https://www.mutopiaproject.org/ftp/MozartWA/AveverumM/AveverumM.mid"),
        Midi("mozart-kv331-tema", "Mozart · K.331 Tema", "W. A. Mozart / Mutopia",
            "https://www.mutopiaproject.org/ftp/MozartWA/KV331/KV331_1_1_tema/KV331_1_1_tema.mid"),
        Midi("chopin-nocturne-op9-2", "Chopin · Nocturne Op.9 No.2", "F. Chopin / Mutopia",
            "https://www.mutopiaproject.org/ftp/ChopinFF/O9/chopin_nocturne_op9_n2/chopin_nocturne_op9_n2.mid"),
        Midi("chopin-mazurka-op6-1", "Chopin · Mazurka Op.6 No.1", "F. Chopin / Mutopia",
            "https://www.mutopiaproject.org/ftp/ChopinFF/O6/Mazurka-Op6-No1/Mazurka-Op6-No1.mid"),
        Midi("anon-old100", "Anonymous · Old 100th", "Anonymous / Mutopia",
            "https://www.mutopiaproject.org/ftp/Anonymous/Old100-orig/Old100-orig.mid"),
        Midi("anon-wenceslas", "Anonymous · Good King Wenceslas", "Anonymous / Mutopia",
            "https://www.mutopiaproject.org/ftp/Anonymous/GoodKingWenceslas/GoodKingWenceslas.mid"),

        // —— Mixkit free SFX (Mixkit License) ——
        Audio("mixkit-2000", "Mixkit SFX 2000", "Mixkit", 2000),
        Audio("mixkit-2004", "Mixkit SFX 2004", "Mixkit", 2004),
        Audio("mixkit-2010", "Mixkit SFX 2010", "Mixkit", 2010),
        Audio("mixkit-2014", "Mixkit SFX 2014", "Mixkit", 2014),
        Audio("mixkit-2020", "Mixkit SFX 2020", "Mixkit", 2020),
        Audio("mixkit-2563", "Mixkit SFX 2563", "Mixkit", 2563),
        Audio("mixkit-2567", "Mixkit SFX 2567", "Mixkit", 2567),
        Audio("mixkit-2568", "Mixkit hit 2568", "Mixkit", 2568),
        Audio("mixkit-2573", "Mixkit SFX 2573", "Mixkit", 2573),
        Audio("mixkit-2575", "Mixkit SFX 2575", "Mixkit", 2575),
        Audio("mixkit-2580", "Mixkit SFX 2580", "Mixkit", 2580),
        Audio("mixkit-3000", "Mixkit SFX 3000", "Mixkit", 3000),
        Audio("mixkit-3003", "Mixkit SFX 3003", "Mixkit", 3003),
        Audio("mixkit-3009", "Mixkit SFX 3009", "Mixkit", 3009),
    ];

    public static IEnumerable<FreeMediaEntry> MidiEntries => All.Where(e => e.Kind == FreeMediaKind.Midi);
    public static IEnumerable<FreeMediaEntry> AudioEntries => All.Where(e => e.Kind == FreeMediaKind.Audio);

    static FreeMediaEntry Midi(string id, string title, string source, string url) =>
        new(id, title, source, FreeMediaKind.Midi, url, "Public Domain / Mutopia", MutopiaHome, $"{id}.mid");

    static FreeMediaEntry Audio(string id, string title, string source, int mixkitId) =>
        new(
            id,
            title,
            source,
            FreeMediaKind.Audio,
            $"https://assets.mixkit.co/active_storage/sfx/{mixkitId}/{mixkitId}-preview.mp3",
            "Mixkit License (free SFX)",
            MixkitLicense,
            $"{id}.mp3");
}
