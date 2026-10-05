namespace FrankMoat.Particles;

internal sealed class DecalField
{
    public const int Capacity = 2800;

    private readonly Decal[] _items = new Decal[Capacity];
    private int _count;
    private int _cursor;

    public int Count => _count;

    public ReadOnlySpan<Decal> Span => _items.AsSpan(0, _count);

    public void Clear()
    {
        _count = 0;
        _cursor = 0;
    }

    public void Add(in Decal decal)
    {
        if (_count < Capacity)
        {
            _items[_count++] = decal;
            _cursor = _count % Capacity;
            return;
        }

        _items[_cursor] = decal;
        _cursor = (_cursor + 1) % Capacity;
    }
}
