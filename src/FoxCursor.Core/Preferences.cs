using System.Text.Json;

namespace FoxCursor.Core;

public sealed record Preferences
{
    public bool Enabled { get; set; } = true;
    public double Size { get; set; } = 80;
    public double Opacity { get; set; } = 1;
    public double OffsetX { get; set; } = 6;
    public double OffsetY { get; set; } = 6;
    public double HotspotX { get; set; } = 0;
    public double HotspotY { get; set; } = 0;
    public double Intensity { get; set; } = 0.65;
    public bool Animations { get; set; } = true;
    public bool TailPhysics { get; set; } = true;
    public bool FacialExpressions { get; set; } = true;
    public bool KeyboardAnimation { get; set; } = true;
    public bool DragAnimation { get; set; } = true;
    public bool IdleAnimation { get; set; } = true;
    public bool LaunchAtStartup { get; set; }
    public bool ShowHotspot { get; set; } = true;

    public void Normalize()
    {
        Size = Limit(Size, 32, 180, 80);
        Opacity = Limit(Opacity, .2, 1, 1);
        OffsetX = Limit(OffsetX, -180, 180, 6);
        OffsetY = Limit(OffsetY, -180, 180, 6);
        HotspotX = Limit(HotspotX, 0, 1, 0);
        HotspotY = Limit(HotspotY, 0, 1, 0);
        Intensity = Limit(Intensity, 0, 1, .65);
    }
    private static double Limit(double x, double min, double max, double fallback) =>
        double.IsFinite(x) ? Math.Clamp(x, min, max) : fallback;
}

public static class PreferenceFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static Preferences Load(string path)
    {
        try
        {
            var value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) ?? new();
            value.Normalize();
            return value;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { return new(); }
    }
    public static void Save(string path, Preferences value)
    {
        value.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, Options));
        File.Move(temporary, path, true);
    }
}
