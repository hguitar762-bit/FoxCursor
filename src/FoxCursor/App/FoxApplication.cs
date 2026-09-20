using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using FoxCursor.Core;
using FoxCursor.DragDrop;
using FoxCursor.Input;
using FoxCursor.Overlay;
using FoxCursor.Safety;
using FoxCursor.Settings;
using Microsoft.Win32;

namespace FoxCursor.App;

internal sealed class FoxApplication : Application
{
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FoxCursor","settings.json");
    private readonly bool startup;
    private readonly AnimationController animation = new();
    private readonly DragObserver drag = new();
    private Preferences preferences = new();
    private GuardianClient? guardian;
    private InputService? input;
    private OverlayWindow? overlay;
    private SettingsWindow? settings;
    private TrayController? tray;
    private DispatcherTimer? heartbeat;
    private bool enabled, rendering, closing, dirty, registeredStartup;
    private double saveAfter, nextFrame;
    private int pointerX, pointerY;
    private double movementX, movementY;
    private bool pointerKnown;

    internal FoxApplication(bool startup)
    {
        this.startup = startup; ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_,e) =>
        {
            e.Handled = true;
            Cleanup();
            MessageBox.Show("FoxCursor 遇到错误并已停止，已尝试恢复系统鼠标。\n\n" + e.Exception.Message, "FoxCursor", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += OnFatalError;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        preferences = PreferenceFile.Load(settingsPath);
        registeredStartup = preferences.LaunchAtStartup = StartupRegistration.IsEnabled();
        guardian = new GuardianClient();
        overlay = new OverlayWindow(preferences);
        tray = new TrayController(preferences, Dispatcher, SetEnabled, SettingsChanged, OpenSettings, () => SetEnabled(false), () => Shutdown());
        heartbeat = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        heartbeat.Tick += Tick; heartbeat.Start();
        SystemEvents.SessionSwitch += SessionChanged;
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        SessionEnding += (_,_) => Cleanup();
        SetEnabled(preferences.Enabled);
        if (!startup) OpenSettings();
    }
    private void SetEnabled(bool value)
    {
        if (closing) return;
        if (value && !enabled)
        {
            try
            {
                input = new InputService(Dispatcher, OnInput);
                animation.Reset(InputService.Now); drag.Reset();
                if (Native.GetCursorPos(out var point)) { pointerX = point.X; pointerY = point.Y; pointerKnown = true; }
                overlay!.Show(); overlay.Move(pointerX,pointerY); overlay.Render(animation.Step(InputService.Now,preferences));
                guardian!.HideCursor();
                enabled = true;
            }
            catch (Exception ex)
            {
                guardian?.RestoreCursor(); input?.Dispose(); input = null; overlay?.Hide(); enabled = false;
                MessageBox.Show("暂时无法启用 FoxCursor，系统鼠标已恢复。\n\n" + ex.Message,"FoxCursor",MessageBoxButton.OK,MessageBoxImage.Warning);
            }
        }
        else if (!value)
        {
            guardian?.RestoreCursor();
            enabled = false; StopFrames();
            input?.Dispose(); input = null; overlay?.Hide(); drag.Reset(); animation.Reset(InputService.Now);
        }
        preferences.Enabled = enabled; tray?.Status(enabled); ScheduleSave();
    }
    private void OpenSettings()
    {
        if (settings is null)
        {
            settings = new SettingsWindow(preferences, SettingsChanged, () => SetEnabled(false));
            settings.Closed += (_,_) => settings = null;
            settings.Show();
        }
        else { settings.WindowState = WindowState.Normal; settings.Activate(); }
    }
    private void SettingsChanged()
    {
        preferences.Normalize();
        if (registeredStartup != preferences.LaunchAtStartup)
        {
            try { StartupRegistration.Set(preferences.LaunchAtStartup); registeredStartup = preferences.LaunchAtStartup; }
            catch (Exception ex)
            {
                preferences.LaunchAtStartup = registeredStartup;
                MessageBox.Show("无法修改开机启动设置：\n" + ex.Message,"FoxCursor");
            }
        }
        overlay?.Apply(preferences);
        if (enabled) RequestFrame();
        ScheduleSave();
    }
    private void ScheduleSave() { dirty = true; saveAfter = InputService.Now + .35; }
    private void Save()
    {
        dirty = false;
        try { PreferenceFile.Save(settingsPath,preferences); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { MessageBox.Show("设置尚未保存到磁盘：\n" + ex.Message,"FoxCursor"); }
    }
    private void Tick(object? sender, EventArgs e)
    {
        if (closing) return;
        if (guardian?.Tick() != true) { Cleanup(); Shutdown(1); return; }
        if (enabled && input?.IsRunning != true) SetEnabled(false);
        double now = InputService.Now;
        if (dirty && now >= saveAfter) Save();
        // The one low-frequency timer also starts occasional idle animation bursts.
        if (enabled && animation.NeedsFrames(now,preferences)) RequestFrame();
    }
    private void OnInput(IReadOnlyList<InputEvent> events)
    {
        if (!enabled || closing) return;
        foreach (var item in events)
        {
            switch (item.Message)
            {
                case 0x0200:
                    if (pointerKnown)
                    {
                        // Integrate physical deltas at the destination DPI; never divide absolute desktop origins by DPI.
                        movementX += (item.X - pointerX) / overlay!.DpiScale;
                        movementY += (item.Y - pointerY) / overlay.DpiScale;
                    }
                    pointerKnown = true; pointerX = item.X; pointerY = item.Y;
                    animation.Move(movementX,movementY,item.Time);
                    if (drag.Move(item.X,item.Y,animation.Held)) animation.BeginDrag(item.Time);
                    break;
                case 0x0201:
                    drag.Down(item.X,item.Y);
                    animation.Pointer(PointerAction.LeftDown,item.Time,item.X,item.Y,Native.GetDoubleClickTime()/1000d,Native.GetSystemMetrics(36),Native.GetSystemMetrics(37));
                    break;
                case 0x0202:
                    animation.Pointer(PointerAction.LeftUp,item.Time,item.X,item.Y); drag.Reset(); break;
                case 0x0204: animation.Pointer(PointerAction.RightDown,item.Time,item.X,item.Y); break;
                case 0x0205: animation.Pointer(PointerAction.RightUp,item.Time,item.X,item.Y); break;
                case 0x020A:
                case 0x020E: animation.Pointer(PointerAction.Wheel,item.Time,item.X,item.Y,wheel:item.Delta); break;
                case 0x9001: animation.Keyboard(item.Time); break;
                case 0x9003: animation.Cancel(item.Time); drag.Reset(); break;
                case 0x000E:
                case 0x8021: if (drag.Observe(item.Message,item.Window)) animation.BeginDrag(item.Time); break;
                case 0x000F:
                case 0x8022:
                case 0x8023: animation.EndDrag(item.Time); drag.Reset(); break;
            }
        }
        RequestFrame();
    }
    private void RequestFrame()
    {
        if (!enabled || rendering) return;
        rendering = true; CompositionTarget.Rendering += Frame;
    }
    private void Frame(object? sender, EventArgs e)
    {
        double now = InputService.Now;
        if (now < nextFrame) return;
        nextFrame = Math.Max(nextFrame + 1d / 60, now);
        overlay!.Move(pointerX,pointerY);
        overlay.Render(animation.Step(now,preferences));
        if (!animation.NeedsFrames(now,preferences)) StopFrames();
    }
    private void StopFrames() { if (!rendering) return; CompositionTarget.Rendering -= Frame; rendering = false; }
    private void SessionChanged(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.RemoteDisconnect or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.SessionLogoff)
            Dispatcher.BeginInvoke(new Action(() => SetEnabled(false)));
    }
    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(() => { if (enabled) overlay?.RefreshDisplay(); }));
    private static void OnFatalError(object sender, UnhandledExceptionEventArgs e) => CursorNative.Restore();
    private static void OnProcessExit(object? sender, EventArgs e) => CursorNative.Restore();
    private void Cleanup()
    {
        if (closing) return;
        closing = true;
        CursorNative.Restore();
        heartbeat?.Stop(); StopFrames();
        input?.Dispose(); input = null;
        guardian?.Dispose(); guardian = null;
        tray?.Dispose(); tray = null;
        overlay?.Close(); overlay = null;
        settings?.Close(); settings = null;
        SystemEvents.SessionSwitch -= SessionChanged;
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        if (dirty) Save();
    }
    protected override void OnExit(ExitEventArgs e) { Cleanup(); base.OnExit(e); }
}
