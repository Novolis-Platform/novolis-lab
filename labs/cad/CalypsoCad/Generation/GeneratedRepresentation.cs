using CalypsoCad.Model;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation;

internal sealed record GeneratedRepresentation(
    PartId PartId,
    CadEntity Entity);
