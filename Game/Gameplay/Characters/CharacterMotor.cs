using System;
using Godot;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 逻辑层：消费意图与物理事实，产出速度、朝向与状态，并广播运动事件。
/// 纯 C# 类，不依赖场景树，可脱离引擎单元测试。
/// 帧流程：Process（物理移动前，根据意图推进）→ PostPhysics（物理移动后，回喂真实结果）。
/// </summary>
public class CharacterMotor
{
    private const float TimerExpired = 999f;

    private readonly CharacterConfig _config;

    public event Action Jumped;
    public event Action Landed;

    public Vector2 Velocity { get; private set; }

    /// <summary>面朝方向：1 右，-1 左。</summary>
    public int Facing { get; private set; } = 1;

    /// <summary>当前运动状态。随时可读，与内部速度、地面事实保持一致。</summary>
    public CharacterState State =>
        !_onFloor
            ? Velocity.Y < 0f
                ? CharacterState.Jump
                : CharacterState.Fall
            : MathF.Abs(Velocity.X) > 1f
                ? CharacterState.Run
                : CharacterState.Idle;

    private bool _onFloor = true;
    private bool _wasJumpHeld;
    private float _timeSinceJumpPressed = TimerExpired;
    private float _timeSinceLeftFloor = TimerExpired;

    public CharacterMotor(CharacterConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>物理移动前调用：根据意图推进计时器并计算本帧速度。</summary>
    /// <param name="intent">本帧意图，来自输入层。</param>
    /// <param name="delta">物理帧时长（秒）。</param>
    /// <param name="gravity">世界重力加速度（像素/秒²，正值向下）。</param>
    public void Process(in InputIntent intent, float delta, float gravity)
    {
        _timeSinceJumpPressed += delta;
        _timeSinceLeftFloor += delta;
        if (intent.JumpPressed)
        {
            _timeSinceJumpPressed = 0f;
        }
        if (_onFloor)
        {
            _timeSinceLeftFloor = 0f;
        }

        if (!_onFloor)
        {
            float fallSpeed = MathF.Min(
                Velocity.Y + gravity * _config.GravityScale * delta,
                _config.MaxFallSpeed
            );
            Velocity = new Vector2(Velocity.X, fallSpeed);
        }

        if (intent.MoveAxis != 0f)
        {
            Facing = intent.MoveAxis > 0f ? 1 : -1;
        }
        float targetX = intent.MoveAxis * _config.MaxSpeed;
        float rate = intent.MoveAxis != 0f ? _config.Acceleration : _config.Friction;
        Velocity = new Vector2(MoveTowards(Velocity.X, targetX, rate * delta), Velocity.Y);

        bool canJump = _onFloor || _timeSinceLeftFloor <= _config.CoyoteTime;
        bool wantsJump = _timeSinceJumpPressed <= _config.JumpBufferTime;
        if (canJump && wantsJump)
        {
            Velocity = new Vector2(Velocity.X, _config.JumpVelocity);
            _onFloor = false;
            _timeSinceLeftFloor = TimerExpired; // 烧掉土狼时间，防止起跳帧内二连跳
            _timeSinceJumpPressed = TimerExpired; // 消费掉跳跃缓冲
            Jumped?.Invoke();
        }

        if (_wasJumpHeld && !intent.JumpHeld && Velocity.Y < 0f)
        {
            Velocity = new Vector2(Velocity.X, Velocity.Y * _config.JumpCutMultiplier);
        }
        _wasJumpHeld = intent.JumpHeld;
    }

    /// <summary>向目标值推进，步长不超过 maxDelta（System.MathF 没有 MoveTowards）。</summary>
    private static float MoveTowards(float current, float target, float maxDelta)
    {
        float diff = target - current;
        return MathF.Abs(diff) <= maxDelta ? target : current + MathF.CopySign(maxDelta, diff);
    }

    /// <summary>物理移动后调用：回喂真实物理结果，修正速度并检测落地。</summary>
    /// <param name="onFloor">MoveAndSlide 之后的 IsOnFloor 结果。</param>
    /// <param name="actualVelocity">MoveAndSlide 碰撞修正后的真实速度。</param>
    public void PostPhysics(bool onFloor, Vector2 actualVelocity)
    {
        bool wasOnFloor = _onFloor;
        _onFloor = onFloor;
        Velocity = actualVelocity;
        if (onFloor)
        {
            _timeSinceLeftFloor = 0f;
            if (Velocity.Y > 0f)
            {
                Velocity = new Vector2(Velocity.X, 0f);
            }
            if (!wasOnFloor)
            {
                Landed?.Invoke();
            }
        }
    }
}
