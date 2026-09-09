using System.Text.Json;
using Godot;

namespace GodotGameTemplate.Persistence;

/// <summary>存档读写（user://save.json）。只在最近重生点变化时写盘。</summary>
public static class SaveStore
{
    private static readonly string Path = "user://save.json";

    public static bool Exists() => FileAccess.FileExists(Path);

    public static void Save(SaveData data)
    {
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        f.StoreString(JsonSerializer.Serialize(data));
    }

    public static bool TryLoad(out SaveData data)
    {
        data = null;
        if (!FileAccess.FileExists(Path))
        {
            return false;
        }
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        data = JsonSerializer.Deserialize<SaveData>(f.GetAsText());
        return data != null;
    }

    public static void Clear() => DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(Path));
}
