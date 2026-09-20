using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using FoxCursor.Character;
using FoxCursor.Core;
using FoxCursor.Input;

namespace FoxCursor.Overlay;

internal sealed class OverlayWindow : Window
{
    private readonly CharacterView character = new();
    private readonly Canvas canvas = new() { IsHitTestVisible = false };
    private readonly Ellipse anchor = new() { Width = 4, Height = 4, Fill = Brushes.White, Stroke = Brushes.Black, StrokeThickness = 1 };
    private nint handle, monitor;
    private double dpi = 1, logicalWidth = 300, logicalHeight = 300, compensation = 1;
    private int pointerX, pointerY;
    private Preferences preferences;
    internal double DpiScale => dpi;
    internal OverlayWindow(Preferences settings)
    {
        preferences = settings;
        Title = "FoxCursor Overlay"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false;
        ShowActivated = false; Focusable = false; Topmost = true; IsHitTestVisible = false;
        Content = canvas; canvas.Children.Add(character); canvas.Children.Add(anchor);
        Width = Height = 300;
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            var source = HwndSource.FromHwnd(handle); source?.AddHook(WindowProc);
            long style = Native.GetWindowLongPtr(handle, -20).ToInt64();
            Native.SetWindowLongPtr(handle, -20, new nint(style | 0x20 | 0x80 | 0x08000000)); // Transparent | ToolWindow | NoActivate.
            Native.SetWindowPos(handle, new nint(-1), 0, 0, 0, 0, 0x0013 | 0x0020);
        };
        Apply(settings);
    }
    private nint WindowProc(nint hwnd, int message, nint w, nint l, ref bool handled)
    {
        if (message == 0x0084) { handled = true; return new nint(-1); } // HTTRANSPARENT.
        if (message == 0x0021) { handled = true; return new nint(3); } // MA_NOACTIVATE.
        if (message == 0x02E0)
            Dispatcher.BeginInvoke(new Action(() => Move(pointerX, pointerY))); // WPF first handles its DPI change.
        return 0;
    }
    internal void Apply(Preferences settings)
    {
        preferences = settings; character.Preferences = settings;
        // Padding accommodates the maximum configurable offset/hotspot without clipping the pointer marker.
        double padding = 210;
        double width = settings.Size * 1.3, height = settings.Size * 1.12;
        Width = logicalWidth = width + padding * 2; Height = logicalHeight = height + padding * 2;
        character.Width = width; character.Height = height;
        Canvas.SetLeft(character, padding); Canvas.SetTop(character, padding);
        Opacity = settings.Opacity;
        anchor.Visibility = settings.ShowHotspot ? Visibility.Visible : Visibility.Hidden;
        Move(pointerX, pointerY); character.InvalidateVisual();
    }
    internal void Move(int x, int y)
    {
        pointerX = x; pointerY = y;
        if (handle == 0) return;
        nint current = Native.MonitorFromPoint(new() { X = x, Y = y }, 2);
        if (current != monitor)
        {
            monitor = current;
            if (Native.GetDpiForMonitor(monitor, 0, out uint dx, out _) == 0) dpi = dx / 96d;
        }
        // A padded window may straddle monitors while the pointer is on the other one.
        // Compensate for WPF's chosen HWND DPI so the character follows pointer-monitor DPI.
        double factor = dpi / VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (Math.Abs(factor - compensation) > .001)
        {
            compensation = factor;
            canvas.RenderTransform = new ScaleTransform(factor,factor);
        }
        double originX = 210 + preferences.Size * (.15 + preferences.HotspotX) - preferences.OffsetX;
        double originY = 210 + preferences.Size * (.19 + preferences.HotspotY * .7) - preferences.OffsetY;
        Canvas.SetLeft(anchor, originX - 2); Canvas.SetTop(anchor, originY - 2);
        int left = (int)Math.Round(x - originX * dpi), top = (int)Math.Round(y - originY * dpi);
        // Physical pixels preserve negative monitor origins; WPF owns content scaling in DIPs.
        if (!Native.SetWindowPos(handle, new nint(-1), left, top,
            (int)Math.Ceiling(logicalWidth * dpi), (int)Math.Ceiling(logicalHeight * dpi), 0x0010 | 0x0200))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    internal void RefreshDisplay() { monitor = 0; Move(pointerX,pointerY); }
    internal void Render(Pose pose) { character.Pose = pose; character.InvalidateVisual(); }
}
