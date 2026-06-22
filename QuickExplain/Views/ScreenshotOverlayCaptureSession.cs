using QuickExplain.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Forms = System.Windows.Forms;
using WpfPoint = System.Windows.Point;

namespace QuickExplain.Views
{
    internal sealed class ScreenshotOverlayCaptureSession
    {
        private readonly ScreenshotOverlayWindow[] _windows;
        private readonly TaskCompletionSource<Rect?> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private WpfPoint _startPointScreen;
        private bool _isCompleted;

        public ScreenshotOverlayCaptureSession()
        {
            var cursor = Forms.Cursor.Position;
            var activeScreen = Forms.Screen.FromPoint(cursor);
            _windows = Forms.Screen.AllScreens
                .Select(screen => new ScreenshotOverlayWindow(this, screen, IsSameScreen(screen, activeScreen)))
                .ToArray();
        }

        public Task<Rect?> CaptureAsync()
        {
            foreach (var window in _windows)
                window.ShowOverlay();

            var activeScreen = Forms.Screen.FromPoint(Forms.Cursor.Position);
            var activeWindow = _windows.FirstOrDefault(window => IsSameScreen(window.Screen, activeScreen))
                ?? _windows.FirstOrDefault();
            if (activeWindow != null)
            {
                activeWindow.Activate();
                WindowUtilities.ForceActive(activeWindow);
                activeWindow.Focus();
            }

            return _tcs.Task;
        }

        public void BeginDrag(WpfPoint startPointScreen)
        {
            if (_isCompleted)
                return;

            _startPointScreen = startPointScreen;
            UpdateDrag(startPointScreen);
        }

        public void UpdateDrag(WpfPoint currentPointScreen)
        {
            if (_isCompleted)
                return;

            var rect = CalculateScreenRect(currentPointScreen);
            foreach (var window in _windows)
                window.UpdateSelection(rect);
        }

        public void EndDrag(WpfPoint endPointScreen)
        {
            if (_isCompleted)
                return;

            var rect = CalculateScreenRect(endPointScreen);
            Complete(rect.Width < 2 || rect.Height < 2 ? null : rect);
        }

        public void CancelCapture()
        {
            Complete(null);
        }

        public void Close()
        {
            foreach (var window in _windows)
            {
                window.ClearSelection();
                if (window.IsVisible)
                    window.Close();
            }
        }

        private void Complete(Rect? result)
        {
            if (_isCompleted)
                return;

            _isCompleted = true;
            foreach (var window in _windows)
                window.ClearSelection();

            _tcs.TrySetResult(result);
        }

        private Rect CalculateScreenRect(WpfPoint currentPointScreen)
        {
            var x = Math.Min(_startPointScreen.X, currentPointScreen.X);
            var y = Math.Min(_startPointScreen.Y, currentPointScreen.Y);
            var width = Math.Abs(_startPointScreen.X - currentPointScreen.X);
            var height = Math.Abs(_startPointScreen.Y - currentPointScreen.Y);
            return new Rect(x, y, width, height);
        }

        private static bool IsSameScreen(Forms.Screen left, Forms.Screen right)
        {
            return string.Equals(left.DeviceName, right.DeviceName, StringComparison.Ordinal);
        }
    }
}
