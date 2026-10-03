#if WINDOWS
using System.Runtime.CompilerServices;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Novolis.IO.Maps;
using Novolis.Maui.Map;
using Windows.System;
using Windows.UI.Core;

namespace Novolis.Lab.MapProviders;

/// <summary>Connects native Windows mouse input to the provider lab map.</summary>
static class WindowsMapInput
{
    static readonly ConditionalWeakTable<FrameworkElement, State> Attached = new();

    /// <summary>Attaches native pointer, wheel, and keyboard handling once per native map view.</summary>
    public static void Attach(MapView map, Action<string>? report = null)
    {
        if (map.Handler?.PlatformView is not FrameworkElement view
            )
        {
            return;
        }

        if (!Attached.TryGetValue(view, out var state))
        {
            state = new State(map, view, report);
            Attached.Add(view, state);
        }

        state.Attach();
    }

    sealed class State(MapView map, FrameworkElement view, Action<string>? report)
    {
        bool _dragging;
        bool _drawingInput;
        bool _tapCandidate;
        bool _attached;
        Windows.Foundation.Point _lastPosition;
        Windows.Foundation.Point _pressPosition;

        public void Attach()
        {
            if (_attached)
                return;

            _attached = true;
            view.PointerPressed += OnPointerPressed;
            view.PointerMoved += OnPointerMoved;
            view.PointerReleased += OnPointerReleased;
            view.PointerCaptureLost += OnPointerCaptureLost;
            view.PointerWheelChanged += OnPointerWheelChanged;
            view.KeyDown += OnKeyDown;
            view.Unloaded += OnUnloaded;
            if (view is Control control)
                control.IsTabStop = true;
        }

        public void OnPointerPressed(
            object sender,
            PointerRoutedEventArgs args)
        {
            if (args.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
                return;

            var point = args.GetCurrentPoint(view);
            if (!point.Properties.IsLeftButtonPressed)
                return;

            if (view is Control control)
                control.Focus(FocusState.Pointer);
            _dragging = true;
            _drawingInput = map.IsDrawing;
            _tapCandidate = true;
            _pressPosition = point.Position;
            _lastPosition = point.Position;
            if (!_drawingInput)
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
            var movedX = point.Position.X - _pressPosition.X;
            var movedY = point.Position.Y - _pressPosition.Y;
            if (_tapCandidate && movedX * movedX + movedY * movedY >= 36)
                _tapCandidate = false;

            if (_drawingInput || _tapCandidate)
            {
                args.Handled = true;
                return;
            }

            map.PanBy(deltaX, deltaY);
            args.Handled = true;
        }

        public void OnPointerReleased(
            object sender,
            PointerRoutedEventArgs args)
        {
            if (!_dragging)
                return;

            var point = args.GetCurrentPoint(view);
            var tap = _tapCandidate;
            _dragging = false;
            if (!_drawingInput)
                map.EndCameraInteraction();
            _drawingInput = false;
            _tapCandidate = false;
            view.ReleasePointerCapture(args.Pointer);
            if (tap)
                map.HandleScreenTap(point.Position.X, point.Position.Y);
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
            if (!_drawingInput)
                map.EndCameraInteraction();
            _drawingInput = false;
            _tapCandidate = false;
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

        public async void OnKeyDown(
            object sender,
            KeyRoutedEventArgs args)
        {
            var controlDown = IsKeyDown(VirtualKey.Control);
            var shiftDown = IsKeyDown(VirtualKey.Shift);
            MapKeyboardCommand? command = null;

            if (controlDown && args.Key == VirtualKey.C)
                command = shiftDown
                    ? MapKeyboardCommand.CopyJson
                    : MapKeyboardCommand.CopyCoordinate;
            else
            {
                command = args.Key switch
                {
                    VirtualKey.Add => MapKeyboardCommand.ZoomIn,
                    VirtualKey.Subtract => MapKeyboardCommand.ZoomOut,
                    VirtualKey.Left => MapKeyboardCommand.PanLeft,
                    VirtualKey.Right => MapKeyboardCommand.PanRight,
                    VirtualKey.Up => MapKeyboardCommand.PanUp,
                    VirtualKey.Down => MapKeyboardCommand.PanDown,
                    VirtualKey.Enter => MapKeyboardCommand.CompleteDrawing,
                    VirtualKey.Escape => MapKeyboardCommand.CancelDrawing,
                    VirtualKey.Delete or VirtualKey.Back => MapKeyboardCommand.EraseSelectedOverlay,
                    _ => null,
                };
            }

            if (command is not { } selected
                || !await map.ExecuteKeyboardCommandAsync(selected))
            {
                return;
            }

            report?.Invoke(selected switch
            {
                MapKeyboardCommand.CopyCoordinate =>
                    "Copied the selected coordinate with Ctrl+C.",
                MapKeyboardCommand.CopyJson =>
                    "Copied the selected coordinate and metadata as JSON.",
                MapKeyboardCommand.CompleteDrawing =>
                    "Drawing completed from the keyboard.",
                MapKeyboardCommand.CancelDrawing =>
                    "Drawing canceled from the keyboard.",
                MapKeyboardCommand.EraseSelectedOverlay =>
                    "Requested erasure of the selected overlay.",
                _ => $"Keyboard command: {selected}.",
            });
            args.Handled = true;
        }

        static bool IsKeyDown(VirtualKey key) =>
            (InputKeyboardSource.GetKeyStateForCurrentThread(key)
                & CoreVirtualKeyStates.Down) != 0;

        public void OnUnloaded(object sender, RoutedEventArgs args) =>
            Detach();

        void Detach()
        {
            if (!_attached)
                return;

            EndDrag();
            view.PointerPressed -= OnPointerPressed;
            view.PointerMoved -= OnPointerMoved;
            view.PointerReleased -= OnPointerReleased;
            view.PointerCaptureLost -= OnPointerCaptureLost;
            view.PointerWheelChanged -= OnPointerWheelChanged;
            view.KeyDown -= OnKeyDown;
            view.Unloaded -= OnUnloaded;
            _attached = false;
        }
    }
}
#endif
