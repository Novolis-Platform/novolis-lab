namespace MusicMakerLab;

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
