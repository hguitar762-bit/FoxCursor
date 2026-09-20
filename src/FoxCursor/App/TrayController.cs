using System.Drawing;
using System.Windows.Threading;
using FoxCursor.Core;
using Forms = System.Windows.Forms;

namespace FoxCursor.App;

internal sealed class TrayController : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Icon mascotIcon;
    private readonly Forms.ContextMenuStrip menu = new();
    private readonly Preferences preferences;
    private readonly Dispatcher dispatcher;
    private readonly Action changed;
    private readonly List<(Forms.ToolStripMenuItem Item, Func<bool> Get)> toggles = new();
    internal TrayController(Preferences preferences, Dispatcher dispatcher, Action<bool> enable,
        Action changed, Action settings, Action restore, Action quit)
    {
        this.preferences = preferences; this.dispatcher = dispatcher; this.changed = changed;
        Add("Enable FoxCursor · 启用", () => enable(true));
        Add("Disable FoxCursor · 暂停", () => enable(false));
        menu.Items.Add(new Forms.ToolStripSeparator());
        Presets("Size · 大小", [48,64,80,110,140], x => preferences.Size = x, x => $"{x:0} DIP");
        Presets("Opacity · 透明度", [.4,.6,.8,1], x => preferences.Opacity = x, x => $"{x:P0}");
        Presets("Animation Intensity · 动画强度", [0,.3,.65,1], x => preferences.Intensity = x, x => $"{x:P0}");
        Toggle("Animations · 动画总开关",()=>preferences.Animations,x=>preferences.Animations=x);
        Toggle("Tail Physics · 叶子物理",()=>preferences.TailPhysics,x=>preferences.TailPhysics=x);
        Toggle("Facial Expressions · 表情",()=>preferences.FacialExpressions,x=>preferences.FacialExpressions=x);
        Toggle("Keyboard Animation · 敲键盘",()=>preferences.KeyboardAnimation,x=>preferences.KeyboardAnimation=x);
        Toggle("Drag Animation · 抱文件",()=>preferences.DragAnimation,x=>preferences.DragAnimation=x);
        Toggle("Idle Animation · 待机",()=>preferences.IdleAnimation,x=>preferences.IdleAnimation=x);
        Toggle("Launch at Startup · 开机启动",()=>preferences.LaunchAtStartup,x=>preferences.LaunchAtStartup=x);
        menu.Items.Add(new Forms.ToolStripSeparator());
        Add("Settings · 设置…",settings);
        Add("Restore System Cursor · 恢复系统鼠标",restore);
        Add("Quit · 退出",quit);
        menu.Opening += (_,_) => { foreach (var (item,get) in toggles) item.Checked = get(); };
        using var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/fox.ico"))!.Stream;
        mascotIcon = new Icon(resource, new Size(32,32));
        icon = new Forms.NotifyIcon { Icon = mascotIcon, Text = "FoxCursor · 狐萝卜", ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_,_) => Post(settings);
    }
    internal void Status(bool enabled) => icon.Text = enabled ? "FoxCursor · 正在陪你工作" : "FoxCursor · 已暂停";
    private void Post(Action action) => dispatcher.BeginInvoke(action);
    private void Add(string title, Action action) { var item = menu.Items.Add(title); item.Click += (_,_) => Post(action); }
    private void Toggle(string title, Func<bool> get, Action<bool> set)
    {
        var item = new Forms.ToolStripMenuItem(title) { Checked = get() };
        item.Click += (_,_) => Post(() => { set(!get()); changed(); });
        toggles.Add((item,get)); menu.Items.Add(item);
    }
    private void Presets(string title, double[] values, Action<double> set, Func<double,string> label)
    {
        var item = new Forms.ToolStripMenuItem(title);
        foreach (double value in values)
        {
            var option = item.DropDownItems.Add(label(value));
            option.Click += (_,_) => Post(() => { set(value); changed(); });
        }
        menu.Items.Add(item);
    }
    public void Dispose() { icon.Visible = false; icon.Dispose(); mascotIcon.Dispose(); menu.Dispose(); }
}
