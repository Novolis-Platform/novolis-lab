namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>Folder + title metadata for one drawing-system category.</summary>
internal sealed record DrawingCategorySpec(
    DrawingCategoryId Id,
    string FolderName,
    string Title,
    string Discipline,
    string Description)
{
    public string SystemCode => ((int)Id).ToString("D3");
}
