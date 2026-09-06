using System;

namespace GodotGameTemplate.Gameplay.Characters.States;

/// <summary>地面移动状态：跑/停/跳跃，并路由冲刺与攻击意图。</summary>
public class GroundedState : CharacterState
{
    public GroundedState(CharacterMotor motor)
        : base(motor) { }

    public override CharacterVisualState VisualState =>
        MathF.Abs(Motor.Velocity.X) > 1f ? CharacterVisualState.Run : CharacterVisualState.Idle;

    public override void Process(in InputIntent intent, float delta, float gravity)
    {
        if (!Motor.IsOnFloor)
        {
            Motor.TransitAirborne(); // 走出平台边缘
            return;
        }

        MoveHorizontally(intent, delta);
        TryJump(intent);
        if (Motor.CurrentState != this)
        {
            return; // 本帧已起跳切入 Airborne，不再处理冲刺/攻击
        }
        RouteCombatActions(intent);
    }
}
