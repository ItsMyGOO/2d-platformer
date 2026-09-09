using Godot;

namespace GodotGameTemplate.Persistence;

/// <summary>
/// 设置持久化（ConfigFile user://settings.cfg）：显示（全屏/垂直同步）与玩法（命中震屏）。
/// 属性为运行时权威值；Load 只在启动时读一次，任何变更走 Set* 即时保存并应用显示项。
/// </summary>
public static class SettingsService
{
    private const string Path = "user://settings.cfg";
    private const string Section = "settings";

    public static bool Fullscreen { get; private set; }
    public static bool Vsync { get; private set; } = true;
    public static bool ScreenshakeEnabled { get; private set; } = true;

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok)
        {
            return;
        }
        Fullscreen = (bool)cfg.GetValue(Section, "fullscreen", false);
        Vsync = (bool)cfg.GetValue(Section, "vsync", true);
        ScreenshakeEnabled = (bool)cfg.GetValue(Section, "screenshake", true);
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue(Section, "fullscreen", Fullscreen);
        cfg.SetValue(Section, "vsync", Vsync);
        cfg.SetValue(Section, "screenshake", ScreenshakeEnabled);
        cfg.Save(Path);
    }

    public static void ApplyDisplay()
    {
        DisplayServer.WindowSetMode(
            Fullscreen
                ? DisplayServer.WindowMode.ExclusiveFullscreen
                : DisplayServer.WindowMode.Windowed
        );
        DisplayServer.WindowSetVsyncMode(
            Vsync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled
        );
    }

    public static void SetFullscreen(bool on)
    {
        Fullscreen = on;
        Save();
        ApplyDisplay();
    }

    public static void SetVsync(bool on)
    {
        Vsync = on;
        Save();
        ApplyDisplay();
    }

    public static void SetScreenshake(bool on)
    {
        ScreenshakeEnabled = on;
        Save();
    }
}
