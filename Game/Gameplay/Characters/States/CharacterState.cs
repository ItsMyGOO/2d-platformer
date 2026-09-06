using System;
using Godot;

namespace GodotGameTemplate.Gameplay.Characters.States;

/// <summary>
/// 行为状态基类。状态对象无跨实例数据（仅自身阶段计时），
/// 共享上下文（速度、朝向、计时器、配置、地面事实）在 Motor 上。
/// </summary>
public abstract class CharacterState
{
    protected CharacterMotor Motor { get; }

    protected CharacterState(CharacterMotor motor) => Motor = motor;

    /// <summary>切入该状态时调用一次（进场动作：清计时、定速度等）。</summary>
    public virtual void Enter() { }

    /// <summary>切出该状态时调用一次（收尾：关判定等）。</summary>
    public virtual void Exit() { }

    /// <summary>推进本状态的行为逻辑，可修改 Motor.Velocity 并发起状态切换。</summary>
    public abstract void Process(in InputIntent intent, float delta, float gravity);

    /// <summary>该状态对应的表现动画。</summary>
    public abstract CharacterVisualState VisualState { get; }

    /// <summary>PostPhysics 检测到「从空中落地」的那一帧回调。</summary>
    public virtual void OnLanded() { }

    /// <summary>重力加速度（受配置倍率与最大下落速度约束）。</summary>
    protected void ApplyGravity(float delta, float gravity)
    {
        float vy = MathF.Min(
            Motor.Velocity.Y + gravity * Motor.Config.GravityScale * delta,
            Motor.Config.MaxFallSpeed
        );
        Motor.Velocity = new Vector2(Motor.Velocity.X, vy);
    }

    /// <summary>常规水平移动：加速逼近 MoveAxis * MaxSpeed，无输入时摩擦减速。</summary>
    protected void MoveHorizontally(in InputIntent intent, float delta)
    {
        if (intent.MoveAxis != 0f)
        {
            Motor.SetFacing(intent.MoveAxis > 0f ? 1 : -1);
        }
        float targetX = intent.MoveAxis * Motor.Config.MaxSpeed;
        float rate = intent.MoveAxis != 0f ? Motor.Config.Acceleration : Motor.Config.Friction;
        Motor.Velocity = new Vector2(
            MoveTowards(Motor.Velocity.X, targetX, rate * delta),
            Motor.Velocity.Y
        );
    }

    /// <summary>尝试跳跃（含土狼时间与跳跃缓冲）。成功时状态机切入 Airborne。</summary>
    protected void TryJump(in InputIntent intent)
    {
        bool canJump = Motor.IsOnFloor || Motor.TimeSinceLeftFloor <= Motor.Config.CoyoteTime;
        bool wantsJump = Motor.TimeSinceJumpPressed <= Motor.Config.JumpBufferTime;
        if (canJump && wantsJump)
        {
            Motor.PerformJump();
        }
    }

    /// <summary>
    /// 路由冲刺/攻击意图。只有 Grounded/Airborne 调用此方法——
    /// Dash/Attack 状态不读这些意图，互斥由状态机结构保证而非散落的 if。
    /// </summary>
    protected void RouteCombatActions(in InputIntent intent)
    {
        if (intent.DashPressed && Motor.TryStartDash())
        {
            return;
        }
        if (intent.AttackPressed)
        {
            Motor.TryStartAttack();
        }
    }

    protected static float MoveTowards(float current, float target, float maxDelta)
    {
        float diff = target - current;
        return MathF.Abs(diff) <= maxDelta ? target : current + MathF.CopySign(maxDelta, diff);
    }
}
