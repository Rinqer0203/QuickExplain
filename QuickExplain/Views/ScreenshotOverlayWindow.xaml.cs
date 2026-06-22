using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace QuickExplain.Views
{
    /// <summary>
    /// ScreenshotOverlayWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class ScreenshotOverlayWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        private readonly ScreenshotOverlayCaptureSession _session;
        private readonly bool _stealthMode;
        private readonly Forms.Screen _screen;
        private bool _dragging;

        internal ScreenshotOverlayWindow(ScreenshotOverlayCaptureSession session, Forms.Screen screen, bool showInstruction)
        {
            _session = session;
            _screen = screen;
            InitializeComponent();
            _stealthMode = Models.AppConfig.Instance.ScreenshotStealthMode;

            SetBoundsToScreen(_screen);
            if (_stealthMode)
            {
                Background = System.Windows.Media.Brushes.Black;
                Opacity = 0.01;
                InstructionText.Visibility = Visibility.Collapsed;
            }
            else
            {
                Cursor = System.Windows.Input.Cursors.Cross;
            }

            if (!showInstruction)
                InstructionText.Visibility = Visibility.Collapsed;

            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            KeyDown += OnKeyDown;
        }

        internal Forms.Screen Screen => _screen;

        internal void ShowOverlay()
        {
            ShowActivated = false;
            Show();
            ApplyNativeBoundsToScreen();
        }

        private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var startPointScreen = PointToScreen(e.GetPosition(this));
            _dragging = true;
            CaptureMouse();
            _session.BeginDrag(startPointScreen);
        }

        private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_dragging)
                return;

            _session.UpdateDrag(PointToScreen(e.GetPosition(this)));
        }

        private void OnMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_dragging)
                return;

            _dragging = false;
            ReleaseMouseCapture();

            _session.EndDrag(PointToScreen(e.GetPosition(this)));
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                _session.CancelCapture();
            }
        }

        internal void ClearSelection()
        {
            _dragging = false;
            if (IsMouseCaptured)
                ReleaseMouseCapture();

            SelectionRect.Visibility = Visibility.Collapsed;
        }

        internal void UpdateSelection(Rect screenRect)
        {
            if (_stealthMode)
                return;

            var bounds = _screen.Bounds;
            var screenBounds = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            var intersection = Rect.Intersect(screenRect, screenBounds);
            if (intersection.IsEmpty || intersection.Width < 1 || intersection.Height < 1)
            {
                SelectionRect.Visibility = Visibility.Collapsed;
                return;
            }

            var topLeft = PointFromScreen(new System.Windows.Point(intersection.Left, intersection.Top));
            var bottomRight = PointFromScreen(new System.Windows.Point(intersection.Right, intersection.Bottom));

            Canvas.SetLeft(SelectionRect, topLeft.X);
            Canvas.SetTop(SelectionRect, topLeft.Y);
            SelectionRect.Width = Math.Max(1, bottomRight.X - topLeft.X);
            SelectionRect.Height = Math.Max(1, bottomRight.Y - topLeft.Y);
            SelectionRect.Visibility = Visibility.Visible;
        }

        private void SetBoundsToScreen(Forms.Screen screen)
        {
            var captureBounds = screen.Bounds;

            var helper = new WindowInteropHelper(this);
            helper.EnsureHandle();
            var source = HwndSource.FromHwnd(helper.Handle);
            var transform = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;

            var topLeft = transform.Transform(new System.Windows.Point(captureBounds.Left, captureBounds.Top));
            var bottomRight = transform.Transform(new System.Windows.Point(captureBounds.Right, captureBounds.Bottom));

            Left = topLeft.X;
            Top = topLeft.Y;
            Width = Math.Max(1, bottomRight.X - topLeft.X);
            Height = Math.Max(1, bottomRight.Y - topLeft.Y);
        }

        private void ApplyNativeBoundsToScreen()
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
                return;

            var bounds = _screen.Bounds;
            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_NOACTIVATE = 0x0010;
            SetWindowPos(
                handle,
                IntPtr.Zero,
                bounds.Left,
                bounds.Top,
                Math.Max(1, bounds.Width),
                Math.Max(1, bounds.Height),
                SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }
}
