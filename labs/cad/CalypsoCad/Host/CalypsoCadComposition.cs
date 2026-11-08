using CalypsoCad.Generation;
using CalypsoCad.Hierarchy;
using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Cad.Generation;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Host;

/// <summary>
/// Thin composition root: CadSessionService + Calypso <see cref="ICadModelGenerator"/>.
/// CLI headless paths do not use this; GUI does.
/// </summary>
internal static class CalypsoCadComposition
{
    public const string GenerateActionId = "generatecalypso";
    public const string GenerateBayActionId = "generatestructuralbay";

    public static CadSessionService CreateSession(string dataRoot)
    {
        Directory.CreateDirectory(dataRoot);
        var settings = new CadEditorSettings(dataRoot);
        settings.Load();
        var document = new CadDocumentSession(settings);
        var bus = new CadCommandBus(document);
        var dispatcher = new CadCommandDispatcher(document, bus, settings);
        var session = new CadSessionService(document, settings, bus, dispatcher)
        {
            AppId = "calypso-cad",
            AppTitle = "Calypso CAD",
            ExportRoot = Path.Combine(dataRoot, "exports"),
        };

        var generator = new CalypsoModelGenerator();
        session.RegisterAction(GenerateActionId, cmd => RunGenerator(session, generator, cmd, structuralBay: false));
        session.RegisterAction(GenerateBayActionId, cmd => RunGenerator(session, generator, cmd, structuralBay: true));
        return session;
    }

    public static CadCommandResultDto RunGenerator(
        CadSessionService session,
        ICadModelGenerator generator,
        CadCommandDto command,
        bool structuralBay)
    {
        if (session.Document.IsDirty)
        {
            // First migration policy: whole-document replace after confirm-if-dirty.
            // CLI/automation may set Properties["confirmDirty"] = "force".
            var force = command.Properties is not null
                && command.Properties.TryGetValue("confirmDirty", out var c)
                && string.Equals(c, "force", StringComparison.OrdinalIgnoreCase);
            if (!force)
            {
                return new CadCommandResultDto
                {
                    Ok = false,
                    ActionId = command.ActionId,
                    Message = "Document is dirty. Save or pass Properties confirmDirty=force to replace.",
                    ErrorCode = "dirty",
                };
            }
        }

        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (structuralBay)
            options["mode"] = "structural-bay";
        if (command.Properties is not null)
        {
            foreach (var (k, v) in command.Properties)
                options[k] = v;
        }

        var result = generator.GenerateAsync(new CadGenerationRequest
        {
            ExistingDocument = session.Document.Document,
            Options = options,
            ReplaceDocument = true,
        }).AsTask().GetAwaiter().GetResult();

        if (!result.Ok)
        {
            return new CadCommandResultDto
            {
                Ok = false,
                ActionId = command.ActionId,
                Message = string.Join("; ", result.Diagnostics),
                ErrorCode = "generateFailed",
            };
        }

        ApplyGeneration(session, result);
        return new CadCommandResultDto
        {
            Ok = true,
            ActionId = command.ActionId,
            Message = result.OutputDirectory ?? "generated",
            Paths = result.OutputDirectory is { } dir ? [dir] : [],
        };
    }

    public static void ApplyGeneration(CadSessionService session, CadGenerationResult result)
    {
        session.Document.ReplaceDocument(result.Document, markDirty: false);
        if (result.Hierarchy is { } hierarchy)
            session.Document.BindHierarchy(hierarchy);
        else
            session.Document.BindHierarchy(ShipHierarchyMapper.ToHierarchy(Publication.CadDocumentAdapter.Active));
        session.Document.Notify();
    }

    public static void LoadCadJson(CadSessionService session, string path)
    {
        session.Document.OpenFromPath(path);
        Publication.CadDocumentAdapter.LoadFromDocument(session.Document.Document);
        session.Document.BindHierarchy(ShipHierarchyMapper.ToHierarchy(Publication.CadDocumentAdapter.Active));
    }
}
