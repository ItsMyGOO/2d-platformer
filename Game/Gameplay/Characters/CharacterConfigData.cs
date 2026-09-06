namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 逻辑层数值配置的运行态（纯 C# POCO）。
/// 编辑器内仍用 CharacterConfig(Resource) 调参，Character._Ready 中经 ToData() 映射为本类型。
/// 拆分原因：Resource 构造依赖引擎原生运行时，纯单元测试进程无法实例化。
/// 各属性语义与 <see cref="CharacterConfig"/> 同名属性一致，数值以编辑态为准。
/// </summary>
public class CharacterConfigData
{
    /// <summary>最大水平移动速度（像素/秒）。</summary>
    public float MaxSpeed { get; init; }

    /// <summary>水平加速（像素/秒²）。</summary>
    public float Acceleration { get; init; }

    /// <summary>无输入时的水平减速（像素/秒²）。</summary>
    public float Friction { get; init; }

    /// <summary>起跳瞬时速度（像素/秒，向上为负）。</summary>
    public float JumpVelocity { get; init; }

    /// <summary>重力倍率。</summary>
    public float GravityScale { get; init; }

    /// <summary>上升中松开跳跃键时保留的上升速度比例（可变跳跃高度）。</summary>
    public float JumpCutMultiplier { get; init; }

    /// <summary>土狼时间：离开地面后仍可起跳的时长（秒）。</summary>
    public float CoyoteTime { get; init; }

    /// <summary>跳跃缓冲：落地前按下跳跃仍会生效的时长（秒）。</summary>
    public float JumpBufferTime { get; init; }

    /// <summary>最大下落速度（像素/秒）。</summary>
    public float MaxFallSpeed { get; init; }

    /// <summary>最大生命值。</summary>
    public int MaxHP { get; init; }

    /// <summary>受击后无敌帧时长（秒）。</summary>
    public float InvincibilityTime { get; init; }

    /// <summary>受击硬直时长（秒）。</summary>
    public float HurtStunTime { get; init; }

    /// <summary>冲刺能力；null 表示不具备。</summary>
    public DashData Dash { get; init; }

    /// <summary>攻击能力；null 表示不具备。</summary>
    public AttackData Attack { get; init; }

    /// <summary>冲刺能力数值，语义同 <see cref="DashConfig"/> 同名属性。</summary>
    public class DashData
    {
        /// <summary>冲刺速度（像素/秒）。</summary>
        public float Speed { get; init; }

        /// <summary>冲刺持续时间（秒）。</summary>
        public float Duration { get; init; }

        /// <summary>冲刺冷却（秒），从进入冲刺时起算。</summary>
        public float Cooldown { get; init; }

        /// <summary>冲刺结束时保留的水平速度比例（衔接移动手感）。</summary>
        public float EndSpeedKeepRatio { get; init; }
    }

    /// <summary>近战攻击数值（三段窗口），语义同 <see cref="AttackConfig"/> 同名属性。</summary>
    public class AttackData
    {
        /// <summary>前摇时长（秒）：出招到判定开启。</summary>
        public float WindupTime { get; init; }

        /// <summary>判定激活时长（秒）。</summary>
        public float ActiveTime { get; init; }

        /// <summary>后摇时长（秒）：判定关闭到恢复可控。</summary>
        public float RecoveryTime { get; init; }

        /// <summary>单次伤害。</summary>
        public int Damage { get; init; }

        /// <summary>击退水平速度（像素/秒）。</summary>
        public float KnockbackHorizontal { get; init; }

        /// <summary>击退垂直速度（像素/秒，正值向上弹起）。</summary>
        public float KnockbackVertical { get; init; }

        /// <summary>攻击冷却（秒），从出招瞬间起算。</summary>
        public float Cooldown { get; init; }
    }
}
