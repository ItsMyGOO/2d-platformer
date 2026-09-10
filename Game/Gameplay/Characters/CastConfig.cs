using Godot;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 剑气数值（HK 式魂量消耗的远程攻击）。配置中为 null 表示不具备剑气能力。
/// 弹速由新月弹场景 Speed 决定，不在此配置。
/// </summary>
public partial class CastConfig : Resource
{
    /// <summary>前摇时长（秒）：施法到弹体出膛。</summary>
    [Export]
    public float WindupTime { get; set; } = 0.08f;

    /// <summary>后摇时长（秒）：出膛到恢复可控。</summary>
    [Export]
    public float RecoveryTime { get; set; } = 0.22f;

    /// <summary>冷却（秒），从施法瞬间起算。</summary>
    [Export]
    public float Cooldown { get; set; } = 0.4f;

    /// <summary>单次伤害。</summary>
    [Export]
    public int Damage { get; set; } = 1;

    /// <summary>击退水平速度（像素/秒）。</summary>
    [Export]
    public float KnockbackHorizontal { get; set; } = 200f;

    /// <summary>击退垂直速度（像素/秒，正值向上弹起）。</summary>
    [Export]
    public float KnockbackVertical { get; set; } = 80f;

    /// <summary>魂量消耗；不足则拒发。</summary>
    [Export]
    public int SoulCost { get; set; } = 11;
}
