using System;
using Godot;

namespace GodotGameTemplate.Gameplay.Characters.States;

/// <summary>死亡终态：仅重力与摩擦生效（尸体落地），无任何转出；玩家由 Reset 重生。</summary>
public class DeadState : CharacterState
{
    public DeadState(CharacterMotor motor)
        : base(motor) { }

    public override void Process(in InputIntent intent, float delta, float gravity)
    {
        ApplyGravity(delta, gravity);
        float vx = MoveTowards(Motor.Velocity.X, 0f, Motor.Config.Friction * delta);
        Motor.Velocity = new Vector2(vx, Motor.Velocity.Y);
    }

    public override CharacterVisualState VisualState => CharacterVisualState.Dead;
}
