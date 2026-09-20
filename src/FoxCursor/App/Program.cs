using System.Windows;
using FoxCursor.Input;
using FoxCursor.Safety;

namespace FoxCursor.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--restore")) return CursorNative.Restore() ? 0 : 1;
        if (args.Length == 2 && args[0] == "--render-preview") return PreviewRenderer.Run(args[1]);
        using var singleton = new Mutex(true, @"Local\FoxCursor.Owner", out bool owner);
        if (!owner) { MessageBox.Show("FoxCursor 已在运行，请在系统托盘中打开设置。", "FoxCursor"); return 0; }
        Native.SetThreadDpiAwarenessContext(new nint(-4));
        try { return new FoxApplication(args.Contains("--startup")).Run(); }
        catch (Exception ex)
        {
            CursorNative.Restore();
            MessageBox.Show("FoxCursor 无法启动，已尝试恢复系统鼠标。\n\n" + ex.Message, "FoxCursor", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
        finally { CursorNative.Restore(); singleton.ReleaseMutex(); }
    }
}
