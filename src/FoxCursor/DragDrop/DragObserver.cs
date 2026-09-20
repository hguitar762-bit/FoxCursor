using System.Text;
using FoxCursor.Input;

namespace FoxCursor.DragDrop;

// Visual observer only. Never registers IDropTarget, reads IDataObject, clipboard or file paths.
internal sealed class DragObserver
{
    private bool candidate;
    private int startX, startY;
    internal bool Active { get; private set; }
    internal void Down(int x, int y)
    {
        startX = x; startY = y;
        candidate = IsShellItemSurface(Native.WindowFromPoint(new() { X = x, Y = y }));
    }
    internal bool Move(int x, int y, bool held)
    {
        if (!held || !candidate || Active) return false;
        if (Math.Abs(x - startX) < Native.GetSystemMetrics(68) && Math.Abs(y - startY) < Native.GetSystemMetrics(69)) return false;
        Active = true;
        return true;
    }
    internal bool Observe(uint eventId, nint window)
    {
        if (eventId is 0x000E or 0x8021)
        {
            if (!IsShellRoot(window)) return false;
            Active = true; return true;
        }
        return false;
    }
    internal void Reset() { Active = candidate = false; }
    private static string Class(nint window)
    {
        var buffer = new StringBuilder(256);
        Native.GetClassName(window, buffer, buffer.Capacity);
        return buffer.ToString();
    }
    private static bool IsShellRoot(nint window)
    {
        if (window == 0) return false;
        string root = Class(Native.GetAncestor(window, 2));
        return root is "CabinetWClass" or "ExploreWClass" or "Progman" or "WorkerW";
    }
    private static bool IsShellItemSurface(nint window) => IsShellRoot(window) &&
        Class(window) is "SysListView32" or "DirectUIHWND";
}
