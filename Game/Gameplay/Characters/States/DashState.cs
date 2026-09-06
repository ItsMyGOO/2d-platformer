using System;
using Godot;

namespace GodotGameTemplate.Gameplay.Characters.States;

/// <summary>
/// 冲刺状态：面朝方向恒速突进，重力关闭；期间忽略跳跃与攻击意图。
/// 可被受击打断；结束按地面事实回到移动状态，保留部分水平速度衔接。
/// </summary>
public class DashState : CharacterState
{
    private float _remainTime;
    private float _direction;

    public DashState(CharacterMotor motor)
        : base(motor) { }

    public override void Enter()
    {
        DashConfig dash = Motor.Config.Dash;
        float axis = Motor.LastIntent.MoveAxis;
        _direction = axis != 0f ? MathF.Sign(axis) : Motor.Facing; // 可反身冲刺
        Motor.SetFacing((int)_direction);
        _remainTime = dash.Duration;
        Motor.Velocity = new Vector2(dash.Speed * _direction, 0f);
    }

    public override void Process(in InputIntent intent, float delta, float gravity)
    {
        // 恒速突进，重力关闭；跳跃/攻击意图一律忽略
        Motor.Velocity = new Vector2(Motor.Config.Dash.Speed * _direction, 0f);
        _remainTime -= delta;
        if (_remainTime <= 0f)
        {
            float keep = Motor.Config.Dash.EndSpeedKeepRatio;
            Motor.Velocity = new Vector2(Motor.Velocity.X * keep, Motor.Velocity.Y);
            Motor.ReturnToLocomotion();
        }
    }

    public override CharacterVisualState VisualState => CharacterVisualState.Dash;
}
