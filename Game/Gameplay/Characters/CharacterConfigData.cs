namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 逻辑层数值配置的运行态（纯 C# POCO）。
/// 编辑器内仍用 CharacterConfig(Resource) 调参，Character._Ready 中经 ToData() 映射为本类型。
/// 拆分原因：Resource 构造依赖引擎原生运行时，纯单元测试进程无法实例化。
/// </summary>
public class CharacterConfigData
{
    public float MaxSpeed { get; init; }

    public float Acceleration { get; init; }

    public float Friction { get; init; }

    public float JumpVelocity { get; init; }

    public float GravityScale { get; init; }

    public float JumpCutMultiplier { get; init; }

    public float CoyoteTime { get; init; }

    public float JumpBufferTime { get; init; }

    public float MaxFallSpeed { get; init; }

    public int MaxHP { get; init; }

    public float InvincibilityTime { get; init; }

    public float HurtStunTime { get; init; }

    /// <summary>冲刺能力；null 表示不具备。</summary>
    public DashData Dash { get; init; }

    /// <summary>攻击能力；null 表示不具备。</summary>
    public AttackData Attack { get; init; }

    public class DashData
    {
        public float Speed { get; init; }

        public float Duration { get; init; }

        public float Cooldown { get; init; }

        public float EndSpeedKeepRatio { get; init; }
    }

    public class AttackData
    {
        public float WindupTime { get; init; }

        public float ActiveTime { get; init; }

        public float RecoveryTime { get; init; }

        public int Damage { get; init; }

        public float KnockbackHorizontal { get; init; }

        public float KnockbackVertical { get; init; }

        public float Cooldown { get; init; }
    }
}
