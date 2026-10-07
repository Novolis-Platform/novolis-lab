namespace AssetStudioLab;

/// <summary>Session over one visual document. Every editor mutates this instance.</summary>
public sealed class StudioDocument
{
    public VisualDocument Document { get; private set; } = VisualDocument.Empty;

    public string Source { get; private set; } = string.Empty;

    public string Stack => VisualStackPrinter.Print(Document);

    public string Csharp => VisualCsharpEmitter.Emit(Document);

    public VisualIr Ir => VisualCompiler.Compile(Document);

    public string FocusName { get; set; } = "NavalSteel";

    public void Load(string source)
    {
        var parsed = VisualLanguageParser.Parse(source);
        if (!parsed.Success)
            throw new VisualParseException(parsed.Errors[0].Message);
        Document = parsed.Document!;
        Source = VisualLanguageFormatter.Format(Document);
        FocusName = Document.Definitions.FirstOrDefault()?.Name ?? FocusName;
    }

    public VisualCommandResult ApplyCommand(string script)
    {
        var result = VisualCommandEditor.Apply(Document, script, FocusName);
        if (result.Success)
        {
            Document = result.Document;
            Source = VisualLanguageFormatter.Format(Document);
        }

        return result;
    }

    public bool TrySetSource(string source, out string error)
    {
        var parsed = VisualLanguageParser.Parse(source);
        if (!parsed.Success)
        {
            error = parsed.Errors[0].Message;
            return false;
        }

        Document = parsed.Document!;
        Source = VisualLanguageFormatter.Format(Document);
        error = string.Empty;
        return true;
    }

    public void SetParameter(string name, double value)
    {
        var definition = Document.Find(FocusName);
        if (definition is null)
            return;
        var args = definition.Parameters.ToDictionary(
            p => p.Name,
            p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                ? VisualExpr.Lit(VisualValue.FromScalar(value))
                : p.Default,
            StringComparer.OrdinalIgnoreCase);
        var instanced = VisualInstantiator.Instantiate(definition, args) with { Name = definition.Name, Parameters = definition.Parameters };
        Document = Document.Replace(instanced);
        Source = VisualLanguageFormatter.Format(Document);
    }
}
