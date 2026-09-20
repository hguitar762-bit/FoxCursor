using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FoxCursor.Core;

namespace FoxCursor.Character;

internal sealed class CharacterView : FrameworkElement
{
    private static readonly Brush Ink = Frozen(new SolidColorBrush(Color.FromRgb(9, 9, 8)));
    private static readonly Brush Cream = Frozen(new SolidColorBrush(Color.FromRgb(253, 239, 205)));
    private static readonly Brush Paper = Frozen(new SolidColorBrush(Color.FromRgb(255, 253, 246)));
    private static readonly Brush Orange = Frozen(new SolidColorBrush(Color.FromRgb(224, 133, 63)));
    private static readonly Pen Outline = Frozen(new Pen(Ink, 2.1) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round });
    private static readonly Pen FacePen = Frozen(new Pen(Ink, 1.9) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
    private static readonly BitmapImage Body = Load("fox-body.png");
    private static readonly BitmapImage Tail = Load("fox-tail.png");
    internal Pose Pose { get; set; } = new(FoxState.Idle, 1, 1, 0, 0, 0, 1, 0, false);
    internal Preferences Preferences { get; set; } = new();
    private static T Frozen<T>(T value) where T : Freezable { value.Freeze(); return value; }
    private static BitmapImage Load(string file)
    {
        var image = new BitmapImage(); image.BeginInit();
        image.UriSource = new Uri("pack://application:,,,/Assets/" + file);
        image.CacheOption = BitmapCacheOption.OnLoad; image.EndInit(); image.Freeze(); return image;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double scale = Math.Min(ActualWidth / 130, ActualHeight / 112);
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.PushTransform(new TranslateTransform(15, 19 + Pose.Bounce));
        dc.PushTransform(new RotateTransform(Pose.Tilt, 32, 45));
        dc.PushTransform(new ScaleTransform(Pose.ScaleX, Pose.ScaleY, 32, 50));
        dc.PushTransform(new RotateTransform(Pose.TailAngle, 59.96, 35.98));
        dc.PushTransform(new ScaleTransform(Pose.TailStretch, 1, 59.96, 35.98));
        dc.DrawImage(Tail, new Rect(0, 0, 100, 70));
        dc.Pop(); dc.Pop();
        dc.DrawImage(Body, new Rect(0, 0, 100, 70));
        if (Preferences.Animations && Preferences.FacialExpressions &&
            (Pose.Blink || Pose.State is FoxState.LeftClick or FoxState.RightClick or FoxState.DoubleClick or FoxState.Charging or FoxState.Dragging or FoxState.KeyboardTyping))
            DrawFace(dc);
        if (Preferences.Animations && Pose.State == FoxState.Dragging && Preferences.DragAnimation) DrawFile(dc);
        if (Preferences.Animations && Pose.State == FoxState.KeyboardTyping && Preferences.KeyboardAnimation) DrawKeyboard(dc);
        dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
    }
    private void DrawFace(DrawingContext dc)
    {
        // Cover only the original facial features with matching body colour.
        dc.DrawGeometry(Cream, null, Geometry.Parse("M 12,18 L 38,15 L 40,36 L 14,39 Z"));
        bool squint = Pose.Blink || Pose.State is FoxState.LeftClick or FoxState.DoubleClick;
        if (squint)
        {
            Stroke(dc, "M 15,25 Q 17,22 19,24"); Stroke(dc, "M 31,22 Q 33,19 35,21");
        }
        else
        {
            dc.DrawEllipse(Ink, null, new Point(17,25), 1.9, Pose.State == FoxState.RightClick ? 1.3 : 2.1);
            dc.DrawEllipse(Ink, null, new Point(33,22), 1.9, 2.1);
        }
        switch (Pose.State)
        {
            case FoxState.Dragging:
            case FoxState.Charging:
            case FoxState.KeyboardTyping:
                Stroke(dc, "M 23,32 Q 26,30 29,31");
                Stroke(dc, "M 14,21 L 20,23"); Stroke(dc, "M 30,20 L 36,17"); break;
            case FoxState.RightClick:
                dc.DrawEllipse(Ink, null, new Point(26,32), 2, 2.6);
                Stroke(dc, "M 30,16 Q 33,14 36,16"); break;
            default:
                dc.DrawGeometry(Ink, null, Geometry.Parse("M 21,30 Q 27,32 31,27 Q 33,36 26,36 Q 21,36 21,30 Z"));
                if (Pose.State == FoxState.DoubleClick) dc.DrawEllipse(Orange, null, new Point(27,34), 2.6, 1.2);
                break;
        }
    }
    private static void Stroke(DrawingContext dc, string path) => dc.DrawGeometry(null, FacePen, Geometry.Parse(path));
    private static void Hand(DrawingContext dc, double x, double y) => dc.DrawEllipse(Cream, Outline, new Point(x,y), 4.2, 3.4);
    private static void DrawFile(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Paper, Outline, new Rect(24,48,21,24), 2, 2);
        dc.DrawGeometry(Cream, Outline, Geometry.Parse("M 37,48 L 45,56 L 37,56 Z"));
        dc.DrawLine(new Pen(Orange, 2), new Point(29,62), new Point(39,62));
        dc.DrawLine(new Pen(Orange, 2), new Point(29,66), new Point(36,66));
        Hand(dc, 24,61); Hand(dc,45,61);
    }
    private void DrawKeyboard(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Paper, Outline, new Rect(13,61,43,14), 3,3);
        for (int row = 0; row < 2; row++)
            for (int column = 0; column < 8; column++)
                dc.DrawRoundedRectangle(Orange, null, new Rect(17 + column * 4.6,64 + row * 4,3,2.5), .6,.6);
        double tap = Math.Sin(Pose.HandPhase) * 2;
        Hand(dc, 23,60 + tap); Hand(dc,45,60 - tap);
    }
}
