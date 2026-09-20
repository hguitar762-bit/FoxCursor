using System.ComponentModel;
using System.Runtime.InteropServices;

namespace FoxCursor.Safety;

internal static class CursorNative
{
    private static readonly uint[] CursorIds = [32512, 32513, 32514, 32515, 32516, 32642, 32643, 32644, 32645, 32646, 32648, 32649, 32650];
    [DllImport("user32.dll", SetLastError = true)] private static extern nint CreateCursor(nint instance, int x, int y, int width, int height, byte[] andMask, byte[] xorMask);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetSystemCursor(nint cursor, uint id);
    [DllImport("user32.dll")] private static extern bool DestroyCursor(nint cursor);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SystemParametersInfo(uint action, uint parameter, nint value, uint flags);
    public static bool Restore() => SystemParametersInfo(0x0057, 0, 0, 0); // Reload the user's saved cursor scheme. No registry mutation.
    public static void Hide()
    {
        byte[] andMask = Enumerable.Repeat((byte)255, 128).ToArray();
        byte[] xorMask = new byte[128];
        try
        {
            foreach (uint id in CursorIds)
            {
                nint cursor = CreateCursor(0, 0, 0, 32, 32, andMask, xorMask);
                if (cursor == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!SetSystemCursor(cursor, id))
                {
                    int error = Marshal.GetLastWin32Error();
                    DestroyCursor(cursor);
                    throw new Win32Exception(error);
                }
                // Ownership transferred to SetSystemCursor; never reuse this handle.
            }
        }
        catch { Restore(); throw; }
    }
}
