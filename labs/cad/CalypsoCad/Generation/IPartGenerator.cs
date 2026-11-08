namespace CalypsoCad.Generation;

internal interface IPartGenerator<in TSpecification>
{
    GenerationResult Generate(TSpecification specification, ShipGeometryContext context);
}
