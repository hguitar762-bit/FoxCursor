using Microsoft.Win32;

namespace FoxCursor.Settings;

internal static class StartupRegistration
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Key);
        return key?.GetValue("FoxCursor") is string;
    }
    internal static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (!enabled) key.DeleteValue("FoxCursor", false);
        else
        {
            string executable = Path.Combine(AppContext.BaseDirectory, "FoxCursor.exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("请先发布或编译 Windows exe，再设置开机启动。", executable);
            key.SetValue("FoxCursor", "\"" + executable + "\" --startup");
        }
    }
}
