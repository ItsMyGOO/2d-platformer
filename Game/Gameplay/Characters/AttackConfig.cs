using Godot;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>近战攻击数值（三段窗口：前摇→判定→后摇）。配置中为 null 表示不具备攻击能力。</summary>
public partial class AttackConfig : Resource
{
    /// <summary>前摇时长（秒）：出招到判定开启。</summary>
    [Export]
    public float WindupTime { get; set; } = 0.12f;

    /// <summary>判定激活时长（秒）。</summary>
    [Export]
    public float ActiveTime { get; set; } = 0.1f;

    /// <summary>后摇时长（秒）：判定关闭到恢复可控。</summary>
    [Export]
    public float RecoveryTime { get; set; } = 0.18f;

    /// <summary>单次伤害。</summary>
    [Export]
    public int Damage { get; set; } = 1;

    /// <summary>击退水平速度（像素/秒）。</summary>
    [Export]
    public float KnockbackHorizontal { get; set; } = 160f;

    /// <summary>击退垂直速度（像素/秒，正值向上弹起）。</summary>
    [Export]
    public float KnockbackVertical { get; set; } = 120f;

    /// <summary>攻击冷却（秒），从出招瞬间起算。</summary>
    [Export]
    public float Cooldown { get; set; } = 0.5f;

    /// <summary>命中反冲速度（像素/秒），攻击者被向面朝反方向弹开。</summary>
    [Export]
    public float RecoilVelocity { get; set; } = 120f;
}
