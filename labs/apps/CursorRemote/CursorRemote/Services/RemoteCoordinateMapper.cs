namespace CursorRemote.Services;

public static class RemoteCoordinateMapper
{
    public static bool TryMap(
        double viewportWidth,
        double viewportHeight,
        int sourceWidth,
        int sourceHeight,
        double viewportX,
        double viewportY,
        out double sourceX,
        out double sourceY,
        double zoom = 1,
        double panX = 0,
        double panY = 0)
    {
        sourceX = 0;
        sourceY = 0;

        if (viewportWidth <= 0
            || viewportHeight <= 0
            || sourceWidth <= 0
            || sourceHeight <= 0
            || zoom <= 0
            || double.IsNaN(zoom)
            || double.IsInfinity(zoom))
        {
            return false;
        }

        // Pinch/pan are applied around the viewport center (matches Image RenderTransform).
        var centerX = viewportWidth / 2;
        var centerY = viewportHeight / 2;
        var untransformedX = (viewportX - centerX - panX) / zoom + centerX;
        var untransformedY = (viewportY - centerY - panY) / zoom + centerY;

        var fitScale = Math.Min(
            viewportWidth / sourceWidth,
            viewportHeight / sourceHeight);
        var renderedWidth = sourceWidth * fitScale;
        var renderedHeight = sourceHeight * fitScale;
        var left = (viewportWidth - renderedWidth) / 2;
        var top = (viewportHeight - renderedHeight) / 2;

        if (untransformedX < left
            || untransformedY < top
            || untransformedX > left + renderedWidth
            || untransformedY > top + renderedHeight)
        {
            return false;
        }

        sourceX = Math.Clamp((untransformedX - left) / fitScale, 0, sourceWidth - 1);
        sourceY = Math.Clamp((untransformedY - top) / fitScale, 0, sourceHeight - 1);
        return true;
    }
}
