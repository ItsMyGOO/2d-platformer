using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace GodotGameTemplate.Persistence;

/// <summary>
/// 键位重映射持久化（user://input_overrides.json：action → physical_keycode）。
/// 只替换动作的键盘事件，保留手柄/鼠标绑定；首次启动快照默认键位供「恢复默认」。
/// </summary>
public static class InputRemapStore
{
    private const string Path = "user://input_overrides.json";

    public static readonly string[] RemappableActions =
    {
        "move_left",
        "move_right",
        "jump",
        "attack",
        "dash",
        "pause",
    };

    private static readonly Dictionary<string, Key> Defaults = new();
    private static readonly Dictionary<string, Key> Overrides = new();

    /// <summary>首次调用时快照各动作的默认键盘键位（供恢复默认）。</summary>
    public static void CaptureDefaults()
    {
        if (Defaults.Count > 0)
        {
            return;
        }
        foreach (var action in RemappableActions)
        {
            if (!InputMap.HasAction(action))
            {
                continue;
            }
            foreach (var e in InputMap.ActionGetEvents(action))
            {
                if (e is InputEventKey key)
                {
                    Defaults[action] = key.PhysicalKeycode;
                    break;
                }
            }
        }
    }

    /// <summary>启动时应用持久化的覆盖（InputMap 原地替换键盘事件）。</summary>
    public static void Apply()
    {
        Load();
        foreach (var (action, key) in Overrides)
        {
            Rebind(action, key);
        }
    }

    /// <summary>当前生效键位（覆盖 > 默认）。</summary>
    public static Key CurrentKey(string action)
    {
        if (Overrides.TryGetValue(action, out var overridden))
        {
            return overridden;
        }
        return Defaults.TryGetValue(action, out var def) ? def : Key.None;
    }

    public static void Set(string action, Key key)
    {
        Overrides[action] = key;
        Rebind(action, key);
        Save();
    }

    /// <summary>恢复默认键位并清除持久化覆盖。</summary>
    public static void Reset()
    {
        Overrides.Clear();
        foreach (var (action, key) in Defaults)
        {
            Rebind(action, key);
        }
        Save();
    }

    /// <summary>只替换键盘事件，保留该动作的手柄/鼠标绑定。</summary>
    private static void Rebind(string action, Key key)
    {
        if (!InputMap.HasAction(action))
        {
            return;
        }
        foreach (var e in InputMap.ActionGetEvents(action))
        {
            if (e is InputEventKey)
            {
                InputMap.ActionEraseEvent(action, e);
            }
        }
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }

    private static void Load()
    {
        Overrides.Clear();
        if (!FileAccess.FileExists(Path))
        {
            return;
        }
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(f.GetAsText());
        if (dict == null)
        {
            return;
        }
        foreach (var (action, code) in dict)
        {
            if (System.Enum.IsDefined(typeof(Key), (ulong)code) && (Key)code != Key.None)
            {
                Overrides[action] = (Key)code;
            }
        }
    }

    private static void Save()
    {
        var dict = new Dictionary<string, int>();
        foreach (var (action, key) in Overrides)
        {
            dict[action] = (int)key;
        }
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        f.StoreString(JsonSerializer.Serialize(dict));
    }
}
