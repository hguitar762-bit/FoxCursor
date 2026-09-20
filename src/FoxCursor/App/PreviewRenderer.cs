using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FoxCursor.Character;
using FoxCursor.Core;
using FoxCursor.Settings;

namespace FoxCursor.App;

// Safe Windows visual QA: renders the real WPF controls, without hooks, guardian or cursor changes.
internal static class PreviewRenderer
{
    internal static int Run(string destination)
    {
        Directory.CreateDirectory(destination);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            var grid = new Grid { Width = 1080, Height = 700, Background = new SolidColorBrush(Color.FromRgb(28,32,30)) };
            for (int i=0;i<3;i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i=0;i<2;i++) grid.RowDefinitions.Add(new RowDefinition());
            FoxState[] states = [FoxState.Idle,FoxState.LeftClick,FoxState.RightClick,FoxState.DoubleClick,FoxState.Dragging,FoxState.KeyboardTyping];
            for (int i=0;i<states.Length;i++)
            {
                var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                panel.Children.Add(new CharacterView { Width = 320, Height = 275,
                    Pose = new(states[i],1,1,0,0,i == 2 ? 12 : 0,1,.7,false) });
                panel.Children.Add(new TextBlock { Text = states[i].ToString(), Foreground = Brushes.Bisque, FontSize = 17, HorizontalAlignment = HorizontalAlignment.Center });
                Grid.SetRow(panel,i/3); Grid.SetColumn(panel,i%3); grid.Children.Add(panel);
            }
            Save(grid,Path.Combine(destination,"character-states.png"),1080,700);
            var window = new SettingsWindow(new Preferences(),()=>{},()=>{}) { ShowActivated = false };
            window.Show(); window.UpdateLayout();
            Save(window,Path.Combine(destination,"settings-window.png"),680,790);
            window.Close();
            return 0;
        }
        finally { app.Shutdown(); }
    }
    private static void Save(FrameworkElement element,string path,int width,int height)
    {
        element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();
        var target = new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);target.Render(element);
        var encoder = new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(target));
        using var output=File.Create(path);encoder.Save(output);
    }
}
