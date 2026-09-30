using System.Text.Json;
using CalypsoCad.Generation;
using CalypsoCad.Models;
using CalypsoInternalsCad.Export;
using Novolis.Cad.Primitives;

namespace CalypsoInternalsCad.Pipeline;

internal sealed record PipelineResult(
    string Directory,
    CadDocument Cad,
    WavefrontObjExporter.MeshExportStats Obj);
