using System;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 意图层：一帧内角色「想做什么」的纯数据描述。
/// 由输入层产生、由逻辑层消费；只描述意图，不含任何执行细节。
/// </summary>
public readonly struct InputIntent
{
    /// <summary>水平移动轴，-1（左）到 1（右），0 表示无移动意图。</summary>
    public float MoveAxis { get; init; }

    /// <summary>本帧是否新按下了跳跃（脉冲信号，仅当帧为真）。</summary>
    public bool JumpPressed { get; init; }

    /// <summary>跳跃键当前是否被按住（用于可变跳跃高度）。</summary>
    public bool JumpHeld { get; init; }

    /// <summary>本帧是否新按下了冲刺（脉冲信号）。</summary>
    public bool DashPressed { get; init; }

    /// <summary>本帧是否新按下了攻击（脉冲信号）。</summary>
    public bool AttackPressed { get; init; }

    /// <summary>本帧是否新按下了剑气（脉冲信号）。</summary>
    public bool CastPressed { get; init; }

    /// <summary>构造一帧意图；MoveAxis 超出 [-1,1] 会被钳制。</summary>
    public static InputIntent Create(
        float moveAxis,
        bool jumpPressed,
        bool jumpHeld,
        bool dashPressed = false,
        bool attackPressed = false,
        bool castPressed = false
    ) =>
        new()
        {
            MoveAxis = Math.Clamp(moveAxis, -1f, 1f),
            JumpPressed = jumpPressed,
            JumpHeld = jumpHeld,
            DashPressed = dashPressed,
            AttackPressed = attackPressed,
            CastPressed = castPressed,
        };
}
