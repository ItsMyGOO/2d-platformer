using Godot;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>冲刺能力数值。角色配置中为 null 表示不具备冲刺能力。</summary>
public partial class DashConfig : Resource
{
    /// <summary>冲刺速度（像素/秒）。</summary>
    [Export]
    public float Speed { get; set; } = 420f;

    /// <summary>冲刺持续时间（秒）。</summary>
    [Export]
    public float Duration { get; set; } = 0.18f;

    /// <summary>冲刺冷却（秒），从进入冲刺时起算。</summary>
    [Export]
    public float Cooldown { get; set; } = 0.6f;

    /// <summary>冲刺结束时保留的水平速度比例（衔接移动手感）。</summary>
    [Export]
    public float EndSpeedKeepRatio { get; set; } = 0.4f;
}
