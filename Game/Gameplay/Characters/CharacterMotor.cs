using System;
using Godot;
using GodotGameTemplate.Gameplay.Characters.Combat;
using GodotGameTemplate.Gameplay.Characters.States;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 逻辑层：类式状态机的宿主与共享上下文。
/// 状态机对外保持 Process / PostPhysics 帧序接口；内部委托当前行为状态。
/// 纯 C# 类，不依赖场景树，可脱离引擎单元测试。
/// </summary>
public class CharacterMotor
{
    private const float TimerExpired = 999f;

    private readonly CharacterConfigData _config;
    private readonly GroundedState _groundedState;
    private readonly AirborneState _airborneState;
    private readonly DashState _dashState;
    private readonly AttackState _attackState;
    private readonly HurtState _hurtState;
    private readonly DeadState _deadState;

    public event Action Jumped;
    public event Action Landed;

    /// <summary>出招瞬间触发（表现层特效 / Hitbox 开新一轮挥击判定）。</summary>
    public event Action AttackStarted;

    /// <summary>攻击判定窗口开/关。逻辑层不碰节点，由编排者订阅驱动 Hitbox。</summary>
    public event Action<bool> AttackActiveChanged;

    public CharacterConfigData Config => _config;

    public Vector2 Velocity { get; internal set; }

    /// <summary>面朝方向：1 右，-1 左。</summary>
    public int Facing { get; private set; } = 1;

    public CharacterState CurrentState { get; private set; }

    public CharacterVisualState VisualState => CurrentState.VisualState;

    public bool IsOnFloor { get; private set; } = true;

    public float TimeSinceJumpPressed { get; private set; } = TimerExpired;

    public float TimeSinceLeftFloor { get; private set; } = TimerExpired;

    /// <summary>上一帧的跳跃键按住状态（供状态类做「松开」沿检测）。</summary>
    public bool WasJumpHeld { get; private set; }

    /// <summary>本帧意图快照，供状态的 Enter 决策使用（如冲刺方向）。</summary>
    public InputIntent LastIntent { get; private set; }

    private float _dashCooldownTimer;
    private float _attackCooldownTimer;

    public CharacterMotor(CharacterConfigData config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _groundedState = new GroundedState(this);
        _airborneState = new AirborneState(this);
        _dashState = new DashState(this);
        _attackState = new AttackState(this);
        _hurtState = new HurtState(this);
        _deadState = new DeadState(this);
        CurrentState = _groundedState;
    }

    /// <summary>物理移动前调用：推进计时器并让当前状态计算本帧速度。</summary>
    public void Process(in InputIntent intent, float delta, float gravity)
    {
        LastIntent = intent;
        UpdateTimers(intent, delta);
        CurrentState.Process(intent, delta, gravity);
        WasJumpHeld = intent.JumpHeld; // 帧末更新，供下帧做「松开」沿检测
    }

    /// <summary>物理移动后调用：回喂真实物理结果，修正速度并检测落地。</summary>
    public void PostPhysics(bool onFloor, Vector2 actualVelocity)
    {
        bool wasOnFloor = IsOnFloor;
        IsOnFloor = onFloor;
        Velocity = actualVelocity;
        if (onFloor)
        {
            TimeSinceLeftFloor = 0f;
            if (Velocity.Y > 0f)
            {
                Velocity = new Vector2(Velocity.X, 0f);
            }
            if (!wasOnFloor)
            {
                CurrentState.OnLanded();
                Landed?.Invoke();
            }
        }
    }

    private void UpdateTimers(in InputIntent intent, float delta)
    {
        TimeSinceJumpPressed += delta;
        if (intent.JumpPressed)
        {
            TimeSinceJumpPressed = 0f;
        }
        TimeSinceLeftFloor += delta;
        if (IsOnFloor)
        {
            TimeSinceLeftFloor = 0f;
        }
        if (_dashCooldownTimer > 0f)
        {
            _dashCooldownTimer -= delta;
        }
        if (_attackCooldownTimer > 0f)
        {
            _attackCooldownTimer -= delta;
        }
    }

    // ==== 状态机内部操作（仅状态类调用）====

    internal void ChangeState(CharacterState next)
    {
        if (next == CurrentState)
        {
            return;
        }
        CurrentState.Exit();
        CurrentState = next;
        next.Enter();
    }

    /// <summary>离地进入空中（走出平台边缘）。</summary>
    internal void TransitAirborne() => ChangeState(_airborneState);

    /// <summary>按当前地面事实回到移动状态（Dash/Attack/Hurt 结束时）。</summary>
    internal void ReturnToLocomotion() => ChangeState(IsOnFloor ? _groundedState : _airborneState);

    /// <summary>执行起跳并烧掉土狼时间与缓冲，防止二连跳。</summary>
    internal void PerformJump()
    {
        Velocity = new Vector2(Velocity.X, _config.JumpVelocity);
        IsOnFloor = false;
        TimeSinceLeftFloor = TimerExpired;
        TimeSinceJumpPressed = TimerExpired;
        Jumped?.Invoke();
        ChangeState(_airborneState);
    }

    /// <summary>尝试进入冲刺。配置缺失或冷却中返回 false。</summary>
    internal bool TryStartDash()
    {
        if (_config.Dash == null || _dashCooldownTimer > 0f)
        {
            return false;
        }
        _dashCooldownTimer = _config.Dash.Cooldown;
        ChangeState(_dashState);
        return true;
    }

    /// <summary>尝试进入攻击。配置缺失或冷却中返回 false。</summary>
    internal bool TryStartAttack()
    {
        if (_config.Attack == null || _attackCooldownTimer > 0f)
        {
            return false;
        }
        _attackCooldownTimer = _config.Attack.Cooldown;
        AttackStarted?.Invoke();
        ChangeState(_attackState);
        return true;
    }

    internal void NotifyAttackActive(bool active) => AttackActiveChanged?.Invoke(active);

    internal void SetFacing(int facing)
    {
        if (facing != 0)
        {
            Facing = facing;
        }
    }

    // ==== 战斗接口（编排者调用）====

    /// <summary>受击：写入击退速度并进入硬直。可打断 Dash/Attack，Dead 除外。</summary>
    public void ForceHurt(DamageInfo info)
    {
        if (CurrentState is DeadState)
        {
            return;
        }
        Velocity = new Vector2(
            info.KnockbackHorizontal * info.SourceDirection,
            -info.KnockbackVertical
        );
        ChangeState(_hurtState);
    }

    /// <summary>死亡：进入终态，仅重力与摩擦生效，由玩家 Reset 解除。</summary>
    public void Kill()
    {
        if (CurrentState is DeadState)
        {
            return;
        }
        Velocity = new Vector2(0f, -140f); // 死亡小跳
        ChangeState(_deadState);
    }

    /// <summary>重生重置：回到地面移动状态并清空全部计时。</summary>
    public void Reset()
    {
        _dashCooldownTimer = 0f;
        _attackCooldownTimer = 0f;
        TimeSinceJumpPressed = TimerExpired;
        TimeSinceLeftFloor = TimerExpired;
        WasJumpHeld = false;
        IsOnFloor = true;
        Velocity = Vector2.Zero;
        ChangeState(_groundedState);
    }
}
