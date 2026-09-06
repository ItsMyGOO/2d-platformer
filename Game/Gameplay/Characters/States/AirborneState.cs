using Godot;

namespace GodotGameTemplate.Gameplay.Characters.States;

/// <summary>空中状态：重力、空中操控、土狼跳、可变跳跃高度，并路由冲刺/攻击。</summary>
public class AirborneState : CharacterState
{
    public AirborneState(CharacterMotor motor)
        : base(motor) { }

    public override CharacterVisualState VisualState =>
        Motor.Velocity.Y < 0f ? CharacterVisualState.Jump : CharacterVisualState.Fall;

    public override void Process(in InputIntent intent, float delta, float gravity)
    {
        ApplyGravity(delta, gravity);
        MoveHorizontally(intent, delta);
        TryJump(intent); // 土狼时间内的缓冲跳

        // 可变跳跃高度：上升中松开跳跃键，一次性削减上升速度
        if (Motor.WasJumpHeld && !intent.JumpHeld && Motor.Velocity.Y < 0f)
        {
            Motor.Velocity = new Vector2(
                Motor.Velocity.X,
                Motor.Velocity.Y * Motor.Config.JumpCutMultiplier
            );
        }

        if (Motor.CurrentState != this)
        {
            return; // 已起跳/切走
        }
        RouteCombatActions(intent);
    }

    public override void OnLanded() => Motor.ReturnToLocomotion();
}
