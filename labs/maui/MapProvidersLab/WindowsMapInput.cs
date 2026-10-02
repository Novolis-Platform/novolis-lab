#if WINDOWS
using System.Runtime.CompilerServices;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Novolis.Maui.Map;

namespace Novolis.Lab.MapProviders;

/// <summary>Connects native Windows mouse input to the provider lab map.</summary>
static class WindowsMapInput
{
    static readonly ConditionalWeakTable<FrameworkElement, State> Attached = new();

    /// <summary>Attaches mouse drag and wheel handling once per native map view.</summary>
    public static void Attach(MapView map)
    {
        if (map.Handler?.PlatformView is not FrameworkElement view
            || Attached.TryGetValue(view, out _))
        {
            return;
        }

        var state = new State(map, view);
        Attached.Add(view, state);
        view.PointerPressed += state.OnPointerPressed;
        view.PointerMoved += state.OnPointerMoved;
        view.PointerReleased += state.OnPointerReleased;
        view.PointerCaptureLost += state.OnPointerCaptureLost;
        view.PointerWheelChanged += state.OnPointerWheelChanged;
    }

    sealed class State(MapView map, FrameworkElement view)
    {
        bool _dragging;
        Windows.Foundation.Point _lastPosition;

        public void OnPointerPressed(
            object sender,
            PointerRoutedEventArgs args)
        {
            if (args.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
                return;

            var point = args.GetCurrentPoint(view);
            if (!point.Properties.IsLeftButtonPressed)
                return;

            _dragging = true;
            _lastPosition = point.Position;
            map.BeginCameraInteraction();
            view.CapturePointer(args.Pointer);
            args.Handled = true;
        }

        public void OnPointerMoved(
            object sender,
            PointerRoutedEventArgs args)
        {
            if (!_dragging)
                return;

            var point = args.GetCurrentPoint(view);
            var deltaX = point.Position.X - _lastPosition.X;
            var deltaY = point.Position.Y - _lastPosition.Y;
            _lastPosition = point.Position;
            map.PanBy(deltaX, deltaY);
            args.Handled = true;
        }

        public void OnPointerReleased(
            object sender,
            PointerRoutedEventArgs args)
        {
            if (!_dragging)
                return;

            _dragging = false;
            map.EndCameraInteraction();
            view.ReleasePointerCapture(args.Pointer);
            args.Handled = true;
        }

        public void OnPointerCaptureLost(
            object sender,
            PointerRoutedEventArgs args) =>
            EndDrag();

        void EndDrag()
        {
            if (!_dragging)
                return;

            _dragging = false;
            map.EndCameraInteraction();
        }

        public void OnPointerWheelChanged(
            object sender,
            PointerRoutedEventArgs args)
        {
            var point = args.GetCurrentPoint(view);
            var delta = point.Properties.MouseWheelDelta;
            if (delta == 0)
                return;

            var zoomDelta = global::System.Math.Clamp(
                delta / 120d * 0.5,
                -2,
                2);
            map.ZoomAt(
                point.Position.X,
                point.Position.Y,
                map.Viewport.Zoom + zoomDelta);
            args.Handled = true;
        }
    }
}
#endif
