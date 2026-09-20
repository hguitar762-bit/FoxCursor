using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FoxCursor.Character;
using FoxCursor.Core;

namespace FoxCursor.Settings;

internal sealed class SettingsWindow : Window
{
    private readonly Preferences preferences;
    private readonly Action changed;
    private readonly CharacterView preview;
    private static readonly Brush Text = new SolidColorBrush(Color.FromRgb(242, 237, 225));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(166, 164, 155));
    internal SettingsWindow(Preferences preferences, Action changed, Action restore)
    {
        this.preferences = preferences; this.changed = changed;
        Title = "FoxCursor · 狐萝卜"; Width = 680; Height = 790; MinWidth = 560; MinHeight = 570;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(28, 32, 30)); Foreground = Text;
        FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 13;
        var root = new DockPanel { Margin = new Thickness(28,24,28,20) }; Content = root;
        var footer = new StackPanel { Margin = new Thickness(0,16,0,0) };
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var restoreButton = new Button { Content = "恢复系统鼠标并暂停", Padding = new Thickness(16,9,16,9), HorizontalAlignment = HorizontalAlignment.Left };
        restoreButton.Click += (_, _) => restore(); footer.Children.Add(restoreButton);
        footer.Children.Add(new TextBlock { Text = "紧急退出  Ctrl + Alt + F12    ·    设置自动保存", Foreground = Muted, Margin = new Thickness(0,10,0,0) });
        var heading = new Grid { Margin = new Thickness(0,0,0,16), Height = 115 };
        heading.ColumnDefinitions.Add(new ColumnDefinition()); heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock { Text = "FOXCURSOR   /   0.1", Foreground = new SolidColorBrush(Color.FromRgb(233,171,106)), FontSize = 11, FontWeight = FontWeights.Bold });
        title.Children.Add(new TextBlock { Text = "让狐萝卜陪你工作", FontSize = 25, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,8,0,8) });
        title.Children.Add(new TextBlock { Text = "轻轻跟随，每一次点击都有回应。", Foreground = Muted }); heading.Children.Add(title);
        preview = new CharacterView { Width = 145, Height = 115, Preferences = preferences };
        Grid.SetColumn(preview,1); heading.Children.Add(preview);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        root.Children.Add(scroll);
        var body = new StackPanel { Margin = new Thickness(0,0,12,0) }; scroll.Content = body;
        Section(body, "外观与定位");
        Slider(body, "角色大小", 32,180, preferences.Size, x => preferences.Size = x, "DIP");
        Slider(body, "不透明度", .2,1, preferences.Opacity, x => preferences.Opacity = x, "%", true);
        Slider(body, "水平偏移", -180,180, preferences.OffsetX, x => preferences.OffsetX = x, "DIP");
        Slider(body, "垂直偏移", -180,180, preferences.OffsetY, x => preferences.OffsetY = x, "DIP");
        Slider(body, "Hotspot X", 0,1, preferences.HotspotX, x => preferences.HotspotX = x, "%", true);
        Slider(body, "Hotspot Y", 0,1, preferences.HotspotY, x => preferences.HotspotY = x, "%", true);
        Toggle(body,"显示实际点击位置的小圆点",preferences.ShowHotspot,x=>preferences.ShowHotspot=x);
        body.Children.Add(new TextBlock { Text = "Hotspot 为角色范围内的相对位置；偏移不会改变 Windows 的真实点击位置。", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0,3,0,10) });
        Section(body, "动作与表情");
        Slider(body,"动画强度",0,1,preferences.Intensity,x=>preferences.Intensity=x,"%",true);
        Toggle(body,"动画总开关",preferences.Animations,x=>preferences.Animations=x);
        Toggle(body,"速度感应叶子 · 弹簧与阻尼",preferences.TailPhysics,x=>preferences.TailPhysics=x);
        Toggle(body,"点击表情",preferences.FacialExpressions,x=>preferences.FacialExpressions=x);
        Toggle(body,"键盘敲击动作 · 不记录输入内容",preferences.KeyboardAnimation,x=>preferences.KeyboardAnimation=x);
        Toggle(body,"拖拽抱文件动作 · 通用文件图标",preferences.DragAnimation,x=>preferences.DragAnimation=x);
        Toggle(body,"待机呼吸与眨眼",preferences.IdleAnimation,x=>preferences.IdleAnimation=x);
        Section(body,"日常使用");
        Toggle(body,"登录 Windows 时启动",preferences.LaunchAtStartup,x=>preferences.LaunchAtStartup=x);
        body.Children.Add(new TextBlock { Text = "关闭此窗口后继续在托盘运行。\nWindows 自绘光标、受保护桌面及部分文件拖拽场景可能不支持。", Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,8), FontSize = 12 });
    }
    private static void Section(Panel panel, string title) => panel.Children.Add(new TextBlock
    { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,18,0,14), Foreground = Text });
    private void Toggle(Panel panel, string label, bool current, Action<bool> set)
    {
        var control = new CheckBox { Content = label, IsChecked = current, Foreground = Text, Margin = new Thickness(0,7,0,7) };
        control.Click += (_,_) => { set(control.IsChecked == true); changed(); preview.InvalidateVisual(); };
        panel.Children.Add(control);
    }
    private void Slider(Panel panel, string label, double min, double max, double current, Action<double> set, string unit, bool percent = false)
    {
        var grid = new Grid { Margin = new Thickness(0,7,0,9) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(77) });
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Foreground = Text });
        var slider = new Slider { Minimum = min, Maximum = max, Value = current, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8,0,12,0), SmallChange = (max-min)/100, LargeChange = (max-min)/10 };
        var value = new TextBlock { Text = Format(current), Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
        Grid.SetColumn(slider,1); Grid.SetColumn(value,2); grid.Children.Add(slider); grid.Children.Add(value);
        slider.ValueChanged += (_,e) => { set(e.NewValue); value.Text = Format(e.NewValue); changed(); preview.InvalidateVisual(); };
        panel.Children.Add(grid);
        string Format(double number) => $"{(percent ? number * 100 : number):0} {unit}";
    }
}
