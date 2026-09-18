using Novolis.Documents;
using Novolis.Markup.Markdown;
using Novolis.Markup.Markdown.Documents;

// Optional: path to an external .md (not copied into the repo).
// Example:
//   dotnet run --project d:\novolis\novolis-lab\labs\documents\HelloMarkdownPdf\HelloMarkdownPdf.csproj -p:NovolisUseProjectReferences=true -- "D:\repos\books\out\the-calypso-cycle\calypso\calypso.md"
var inputPath = args.ElementAtOrDefault(0);
var isExternal = !string.IsNullOrWhiteSpace(inputPath) && File.Exists(inputPath);

var outDir = Path.GetFullPath(Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".novolis",
    "artifacts",
    isExternal ? "calypso-documents-pdf" : "hello-markdown-pdf"));
Directory.CreateDirectory(outDir);
var pdfPath = Path.Combine(outDir, isExternal ? "calypso-documents.pdf" : "hello-markdown.pdf");

if (isExternal)
{
    var markdown = await File.ReadAllTextAsync(inputPath!);
    var options = new MarkdownPagedExportOptions
    {
        Title = "The Calypso Cycle",
        Author = "Novolis",
        IncludeCover = false,
        IncludeToc = false,
        HeaderTemplate = "{chapter}",
        UseChapterTitleHeader = true,
        FooterTemplate = "{page}",
        Typography = new Typography
        {
            BodyFontSizePt = 11f,
            H1SizePt = 18f,
            TableFontSizePt = 8.5f,
            LineHeight = 1.35f,
            AfterLevel1SpacingPt = 14f,
            ParagraphSpacingPt = 8f,
        },
        TextBox = new TextBoxBlock
        {
            PaddingPt = 6f,
            BorderStrokePt = 0.8f,
            BorderColor = DocumentColor.Gray,
            Background = DocumentColor.LightGray,
            FontSizePt = 8.5f,
            LineHeight = 1.22f,
            LineGapPt = 1.5f,
            TextColor = DocumentColor.Gray,
        },
    };

    // External path may already be under artifacts; keep one predictable PDF name for chapter slices.
    if (Path.GetFileName(inputPath!).StartsWith("chapter-", StringComparison.OrdinalIgnoreCase))
        pdfPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(inputPath) + ".pdf");

    Console.WriteLine($"Reading {inputPath}");
    MarkdownDocumentPdfExporter.ExportToFile(markdown, pdfPath, options);
}
else
{
    var document = new MarkdownDocument()
        .WithHeader("Duckville Harbor")
        .With(new MarkdownParagraph().WithText(
            "Freight bells marked the hour across the strait while spray bit the quay stones."))
        .WithHeader("Quay-side", 2)
        .With(new MarkdownParagraph().WithText(
            "Captain Rook checked the manifest twice, then folded the chart into his coat as fog settled low over the channel markers."))
        .With(new MarkdownHorizontalRule())
        .WithHeader("Lien notes", 3)
        .WithUnorderedList("Bonded cargo waited in the shed", "The tide turned toward the open reach")
        .WithOrderedList("Check the bond ledger", "Cast off at first light")
        .WithHeader("Manifest")
        .With(new MarkdownParagraph().WithText(
            "The river ran cold through Duckville. Harbor lights winked across the bonded shed while freight bells counted the watch."))
        .WithHeader("Departure")
        .With(new MarkdownParagraph().WithText(
            "Morning light found the bridge empty and the channel clear for a Calypso tramp outbound."));

    MarkdownDocumentPdfExporter.ExportToFile(document, pdfPath, new MarkdownPagedExportOptions
    {
        Title = "Duckville Harbor",
        Author = "Novolis",
    });
}

var bytes = await File.ReadAllBytesAsync(pdfPath);
Console.WriteLine(pdfPath);
Console.WriteLine($"Bytes: {bytes.Length}");
return bytes.Length > 1000 ? 0 : 1;
