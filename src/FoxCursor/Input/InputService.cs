using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace FoxCursor.Input;

internal readonly record struct InputEvent(uint Message, int X, int Y, int Delta, double Time, nint Window = 0);

internal sealed class InputService : IDisposable
{
    private readonly object gate = new();
    private readonly Queue<InputEvent> queue = new();
    private readonly Dispatcher dispatcher;
    private readonly Action<IReadOnlyList<InputEvent>> consume;
    private readonly Native.HookProc mouseProc, keyboardProc;
    private readonly Native.WinEventProc eventProc;
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new();
    private nint mouse, keyboard, systemDrag, objectDrag;
    private uint threadId;
    private Exception? startupError;
    private InputEvent? latestMove;
    private bool posted, disposed;
    internal static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    internal bool IsRunning => thread.IsAlive && startupError is null;

    internal InputService(Dispatcher dispatcher, Action<IReadOnlyList<InputEvent>> consume)
    {
        this.dispatcher = dispatcher; this.consume = consume;
        mouseProc = Mouse; keyboardProc = Keyboard; eventProc = WinEvent;
        thread = new Thread(Run) { IsBackground = true, Name = "FoxCursor input (no input logging)" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!ready.Wait(5000)) { Dispose(); throw new TimeoutException("输入监听启动超时。"); }
        if (startupError is not null) { Dispose(); throw new InvalidOperationException("无法安装输入监听。", startupError); }
    }
    private void Run()
    {
        try
        {
            Native.SetThreadDpiAwarenessContext(new nint(-4));
            threadId = Native.GetCurrentThreadId();
            Native.PeekMessage(out _, 0, 0, 0, 0); // Create the thread's message queue before publishing ready.
            nint module = Native.GetModuleHandle(null);
            mouse = Native.SetWindowsHookEx(14, mouseProc, module, 0);
            keyboard = Native.SetWindowsHookEx(13, keyboardProc, module, 0);
            if (mouse == 0 || keyboard == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            systemDrag = Native.SetWinEventHook(0x000E, 0x000F, 0, eventProc, 0, 0, 2);
            objectDrag = Native.SetWinEventHook(0x8021, 0x8023, 0, eventProc, 0, 0, 2);
            ready.Set();
            lock (gate) { if (disposed) return; }
            while (Native.GetMessage(out var message, 0, 0, 0) > 0)
            { Native.TranslateMessage(ref message); Native.DispatchMessage(ref message); }
        }
        catch (Exception ex) { startupError = ex; ready.Set(); }
        finally
        {
            if (mouse != 0) Native.UnhookWindowsHookEx(mouse);
            if (keyboard != 0) Native.UnhookWindowsHookEx(keyboard);
            if (systemDrag != 0) Native.UnhookWinEvent(systemDrag);
            if (objectDrag != 0) Native.UnhookWinEvent(objectDrag);
        }
    }
    private nint Mouse(int code, nint w, nint l)
    {
        if (code >= 0)
        {
            try
            {
                var data = Marshal.PtrToStructure<Native.MouseInfo>(l);
                Publish(new((uint)w, data.Point.X, data.Point.Y, (short)(data.MouseData >> 16), Now));
            }
            catch { /* Never let a managed exception cross the native hook boundary. */ }
        }
        return Native.CallNextHookEx(0, code, w, l);
    }
    private nint Keyboard(int code, nint w, nint l)
    {
        if (code >= 0)
        {
            try
            {
                // Examine only the virtual-key number locally to exclude modifiers and handle Escape.
                // No character translation, key identifiers in queues, key history, or input logs.
                int key = Marshal.ReadInt32(l);
                bool down = (uint)w is 0x0100 or 0x0104;
                if (key == 0x1B && down) Publish(new(0x9003, 0, 0, 0, Now));
                else if (key is not (0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5))
                    Publish(new(down ? 0x9001u : 0x9002u, 0, 0, 0, Now));
            }
            catch { }
        }
        return Native.CallNextHookEx(0, code, w, l);
    }
    private void WinEvent(nint hook, uint id, nint window, int obj, int child, uint sourceThread, uint time)
    { try { Publish(new(id, 0, 0, 0, Now, window)); } catch { } }
    private void Publish(InputEvent value)
    {
        lock (gate)
        {
            if (disposed) return;
            if (value.Message == 0x0200) latestMove = value;
            else
            {
                if (queue.Count >= 256)
                {
                    // A stalled UI must not replay old input or keep a held button stuck.
                    queue.Clear(); queue.Enqueue(new(0x9003, 0, 0, 0, value.Time));
                }
                queue.Enqueue(value);
            }
            if (posted) return;
            posted = true;
            dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(Drain));
        }
    }
    private void Drain()
    {
        List<InputEvent> batch;
        lock (gate)
        {
            posted = false;
            if (disposed) return;
            batch = new(queue); queue.Clear();
            if (latestMove is { } move) { batch.Add(move); latestMove = null; }
        }
        batch.Sort((a, b) => a.Time.CompareTo(b.Time));
        consume(batch);
    }
    public void Dispose()
    {
        lock (gate) { if (disposed) return; disposed = true; queue.Clear(); latestMove = null; }
        if (threadId != 0) Native.PostThreadMessage(threadId, 0x0012, 0, 0);
        if (Thread.CurrentThread != thread && !thread.Join(2000))
        {
            if (mouse != 0) Native.UnhookWindowsHookEx(mouse);
            if (keyboard != 0) Native.UnhookWindowsHookEx(keyboard);
        }
        else if (!thread.IsAlive) ready.Dispose();
        // Keep delegates and ready alive until the thread has actually returned.
    }
}
