using System;
using Godot;

namespace GodotGameTemplate.Gameplay.Characters.States;

/// <summary>受击硬直状态：击退速度自然衰减，期间忽略一切主动输入。</summary>
public class HurtState : CharacterState
{
    private float _remainTime;

    public HurtState(CharacterMotor motor)
        : base(motor) { }

    public override void Enter() => _remainTime = Motor.Config.HurtStunTime;

    public override void Process(in InputIntent intent, float delta, float gravity)
    {
        ApplyGravity(delta, gravity);
        float vx = MoveTowards(Motor.Velocity.X, 0f, Motor.Config.Friction * 0.5f * delta);
        Motor.Velocity = new Vector2(vx, Motor.Velocity.Y);
        _remainTime -= delta;
        if (_remainTime <= 0f)
        {
            Motor.ReturnToLocomotion();
        }
    }

    public override CharacterVisualState VisualState => CharacterVisualState.Hurt;
}
