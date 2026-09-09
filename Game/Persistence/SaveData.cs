namespace GodotGameTemplate.Persistence;

/// <summary>存档数据（纯 C#，可单测）。当前仅记录最近重生点；version 留作收集/能力扩展位。</summary>
public class SaveData
{
    public int Version { get; set; } = 1;
    public float SpawnX { get; set; }
    public float SpawnY { get; set; }
}
