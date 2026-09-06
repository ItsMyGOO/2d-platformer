using Godot;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 逻辑层全部数值参数。玩家与敌人各持一份配置资源，
/// 二者共用同一套状态机代码，仅数值与能力（Dash/Attack 为 null 即无此能力）不同。
/// </summary>
public partial class CharacterConfig : Resource
{
    /// <summary>最大水平移动速度（像素/秒）。</summary>
    [Export]
    public float MaxSpeed { get; set; } = 130f;

    /// <summary>水平加速（像素/秒²）。</summary>
    [Export]
    public float Acceleration { get; set; } = 1000f;

    /// <summary>无输入时的水平减速（像素/秒²）。</summary>
    [Export]
    public float Friction { get; set; } = 1400f;

    /// <summary>起跳瞬时速度（像素/秒，向上为负）。</summary>
    [Export]
    public float JumpVelocity { get; set; } = -330f;

    /// <summary>重力倍率。</summary>
    [Export]
    public float GravityScale { get; set; } = 1f;

    /// <summary>上升中松开跳跃键时保留的上升速度比例（可变跳跃高度）。</summary>
    [Export]
    public float JumpCutMultiplier { get; set; } = 0.45f;

    /// <summary>土狼时间：离开地面后仍可起跳的时长（秒）。</summary>
    [Export]
    public float CoyoteTime { get; set; } = 0.1f;

    /// <summary>跳跃缓冲：落地前按下跳跃仍会生效的时长（秒）。</summary>
    [Export]
    public float JumpBufferTime { get; set; } = 0.12f;

    /// <summary>最大下落速度（像素/秒）。</summary>
    [Export]
    public float MaxFallSpeed { get; set; } = 520f;

    /// <summary>最大生命值。</summary>
    [Export]
    public int MaxHP { get; set; } = 5;

    /// <summary>受击后无敌帧时长（秒）。</summary>
    [Export]
    public float InvincibilityTime { get; set; } = 0.8f;

    /// <summary>受击硬直时长（秒）。</summary>
    [Export]
    public float HurtStunTime { get; set; } = 0.3f;

    /// <summary>冲刺能力配置；null 表示不具备冲刺。</summary>
    [Export]
    public DashConfig Dash { get; set; }

    /// <summary>攻击能力配置；null 表示不具备攻击。</summary>
    [Export]
    public AttackConfig Attack { get; set; }

    /// <summary>映射为逻辑层运行态配置（见 <see cref="CharacterConfigData"/>）。</summary>
    public CharacterConfigData ToData() =>
        new()
        {
            MaxSpeed = MaxSpeed,
            Acceleration = Acceleration,
            Friction = Friction,
            JumpVelocity = JumpVelocity,
            GravityScale = GravityScale,
            JumpCutMultiplier = JumpCutMultiplier,
            CoyoteTime = CoyoteTime,
            JumpBufferTime = JumpBufferTime,
            MaxFallSpeed = MaxFallSpeed,
            MaxHP = MaxHP,
            InvincibilityTime = InvincibilityTime,
            HurtStunTime = HurtStunTime,
            Dash =
                Dash == null
                    ? null
                    : new CharacterConfigData.DashData
                    {
                        Speed = Dash.Speed,
                        Duration = Dash.Duration,
                        Cooldown = Dash.Cooldown,
                        EndSpeedKeepRatio = Dash.EndSpeedKeepRatio,
                    },
            Attack =
                Attack == null
                    ? null
                    : new CharacterConfigData.AttackData
                    {
                        WindupTime = Attack.WindupTime,
                        ActiveTime = Attack.ActiveTime,
                        RecoveryTime = Attack.RecoveryTime,
                        Damage = Attack.Damage,
                        KnockbackHorizontal = Attack.KnockbackHorizontal,
                        KnockbackVertical = Attack.KnockbackVertical,
                        Cooldown = Attack.Cooldown,
                    },
        };
}
