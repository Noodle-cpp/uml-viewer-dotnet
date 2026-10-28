using Avalonia;
using Avalonia.Input;
using UmlViewer.Engine;

namespace UmlViewer.App;

public sealed class CanvasContextEventArgs : EventArgs
{
    public PointerPressedEventArgs PointerEvent { get; }
    public HitResult? Hit { get; }
    public Point ScreenPoint { get; }

    public CanvasContextEventArgs(PointerPressedEventArgs pointerEvent, HitResult? hit, Point screenPoint)
    {
        PointerEvent = pointerEvent;
        Hit = hit;
        ScreenPoint = screenPoint;
    }
}
