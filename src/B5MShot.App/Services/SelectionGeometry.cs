using System.Windows;

namespace B5MShot.App.Services;

[Flags]
public enum SelectionEdge { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8, Move = 16 }

public static class SelectionGeometry
{
    // Work in source pixels, never in a monitor's logical DPI coordinates.
    public static Int32Rect Change(Int32Rect start, SelectionEdge edge, int dx, int dy, int width, int height)
    {
        if (edge == SelectionEdge.Move)
            return new(Math.Clamp(start.X + dx, 0, width - start.Width),
                Math.Clamp(start.Y + dy, 0, height - start.Height), start.Width, start.Height);
        var left = start.X; var top = start.Y;
        var right = left + start.Width; var bottom = top + start.Height;
        var minWidth = Math.Min(4, start.Width); var minHeight = Math.Min(4, start.Height);
        if (edge.HasFlag(SelectionEdge.Left)) left = Math.Clamp(left + dx, 0, right - minWidth);
        if (edge.HasFlag(SelectionEdge.Right)) right = Math.Clamp(right + dx, left + minWidth, width);
        if (edge.HasFlag(SelectionEdge.Top)) top = Math.Clamp(top + dy, 0, bottom - minHeight);
        if (edge.HasFlag(SelectionEdge.Bottom)) bottom = Math.Clamp(bottom + dy, top + minHeight, height);
        return new(left, top, right - left, bottom - top);
    }
}
