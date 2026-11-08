namespace ArtillerySimulator;

internal readonly record struct FireControlUiButton(
    string Id,
    float X,
    float Y,
    float W,
    float H,
    string Label,
    bool Selected)
{
    public bool Contains(float mx, float my) =>
        mx >= X && my >= Y && mx <= X + W && my <= Y + H;
}
