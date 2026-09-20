using System.Diagnostics;
using FoxCursor.Safety;

namespace FoxCursor.App;

internal sealed class GuardianClient : IDisposable
{
    private readonly EventWaitHandle ready, pulse, active, stop, emergency;
    private readonly Process process;
    private bool disposed;
    internal GuardianClient()
    {
        string token = Guid.NewGuid().ToString("N");
        string prefix = @"Local\FoxCursor." + token;
        ready = new(false, EventResetMode.ManualReset, prefix + ".ready");
        pulse = new(false, EventResetMode.AutoReset, prefix + ".pulse");
        active = new(false, EventResetMode.ManualReset, prefix + ".active");
        stop = new(false, EventResetMode.ManualReset, prefix + ".stop");
        emergency = new(false, EventResetMode.ManualReset, prefix + ".emergency");
        using var current = Process.GetCurrentProcess();
        string executable = Path.Combine(AppContext.BaseDirectory, "FoxCursor.Guardian.exe");
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add(Environment.ProcessId.ToString());
        info.ArgumentList.Add(current.StartTime.ToUniversalTime().Ticks.ToString());
        info.ArgumentList.Add(token);
        try
        {
            process = Process.Start(info) ?? throw new InvalidOperationException("无法启动光标恢复守护进程。");
            if (!ready.WaitOne(5000) || process.HasExited)
                throw new InvalidOperationException("安全守护进程未就绪。请检查 Ctrl+Alt+F12 是否被其他程序占用，以及 Guardian 文件是否齐全。");
        }
        catch
        {
            stop.Set();
            ready.Dispose(); pulse.Dispose(); active.Dispose(); stop.Dispose(); emergency.Dispose();
            throw;
        }
    }
    internal bool Tick()
    {
        if (disposed || process.HasExited || emergency.WaitOne(0)) return false;
        pulse.Set(); return true;
    }
    internal void HideCursor()
    {
        if (!Tick()) throw new InvalidOperationException("安全守护进程已停止。");
        active.Set(); // Arm recovery before touching any system cursor.
        CursorNative.Hide();
    }
    internal void RestoreCursor()
    {
        if (CursorNative.Restore()) active.Reset();
        // If reload fails, keep guardian armed so it retries on exit.
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        RestoreCursor(); stop.Set();
        process.WaitForExit(2000);
        process.Dispose();
        ready.Dispose(); pulse.Dispose(); active.Dispose(); stop.Dispose(); emergency.Dispose();
    }
}
