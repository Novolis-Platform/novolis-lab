namespace CalypsoCad.Generation.Fabrication;

internal static class SheetNester
{
    public static List<(string SheetId, List<FabNestRow> Rows)> Pack(
        IReadOnlyList<NestItem> items,
        float sheetW,
        float sheetH,
        float margin = 0.05f)
    {
        var sorted = items.OrderByDescending(i => MathF.Max(i.Width, i.Height)).ToList();
        var sheets = new List<(string SheetId, List<FabNestRow> Rows)>();
        var sheetIndex = 0;
        float cursorX = margin;
        float cursorY = margin;
        float rowH = 0f;
        var current = new List<FabNestRow>();

        foreach (var item in sorted)
        {
            var w = item.Width + margin;
            var h = item.Height + margin;
            if (cursorX + w > sheetW - margin)
            {
                cursorX = margin;
                cursorY += rowH + margin;
                rowH = 0f;
            }

            if (cursorY + h > sheetH - margin)
            {
                sheets.Add(($"sheet-{sheetIndex:00}", current));
                sheetIndex++;
                current = [];
                cursorX = margin;
                cursorY = margin;
                rowH = 0f;
            }

            current.Add(new FabNestRow(item.PartId, cursorX, cursorY, 0f, item.Profile));
            cursorX += w;
            rowH = MathF.Max(rowH, h);
        }

        if (current.Count > 0)
            sheets.Add(($"sheet-{sheetIndex:00}", current));
        return sheets;
    }

    internal sealed record FabNestRow(string PartId, float X, float Y, float RotDeg, List<(float X, float Y)> Profile);
}
