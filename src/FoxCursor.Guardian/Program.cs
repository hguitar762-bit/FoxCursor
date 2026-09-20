using System.Diagnostics;
using System.Runtime.InteropServices;
using FoxCursor.Safety;

namespace FoxCursor.Guardian;

internal static class Program
{
    [StructLayout(LayoutKind.Sequential)] private struct Message
    { public nint Window; public uint Id; public nuint WParam; public nint LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, nint window, uint min, uint max, uint remove);

    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--restore") return CursorNative.Restore() ? 0 : 1;
        if (args.Length != 3 || !int.TryParse(args[0], out int pid) || !long.TryParse(args[1], out long ticks) ||
            !Guid.TryParseExact(args[2], "N", out _)) return 2;
        bool armed = false;
        try
        {
            using var parent = Process.GetProcessById(pid);
            if (parent.StartTime.ToUniversalTime().Ticks != ticks) return 2;
            string prefix = @"Local\FoxCursor." + args[2];
            using var ready = EventWaitHandle.OpenExisting(prefix + ".ready");
            using var pulse = EventWaitHandle.OpenExisting(prefix + ".pulse");
            using var active = EventWaitHandle.OpenExisting(prefix + ".active");
            using var stop = EventWaitHandle.OpenExisting(prefix + ".stop");
            using var emergency = EventWaitHandle.OpenExisting(prefix + ".emergency");
            if (!RegisterHotKey(0, 1, 0x4003, 0x7B)) return 3; // NoRepeat | Ctrl | Alt, F12.
            try
            {
                ready.Set();
                var clock = Stopwatch.StartNew();
                long lastPulse = clock.ElapsedMilliseconds;
                while (true)
                {
                    armed = active.WaitOne(0);
                    if (parent.HasExited || stop.WaitOne(0)) break;
                    if (pulse.WaitOne(0)) lastPulse = clock.ElapsedMilliseconds;
                    bool panic = false;
                    while (PeekMessage(out var message, 0, 0, 0, 1))
                        panic |= message.Id == 0x0312;
                    if (panic || clock.ElapsedMilliseconds - lastPulse > 5000)
                    {
                        if (armed) CursorNative.Restore();
                        emergency.Set();
                        // Let a healthy UI dispose hooks. Terminate a hung owner only after cursor recovery.
                        if (!parent.WaitForExit(1500)) parent.Kill();
                        break;
                    }
                    stop.WaitOne(100); // Safety heartbeat only; never polls keyboard or mouse position.
                }
            }
            finally { UnregisterHotKey(0, 1); }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or WaitHandleCannotBeOpenedException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        { return 4; }
        finally { if (armed) CursorNative.Restore(); }
        return 0;
    }
}
