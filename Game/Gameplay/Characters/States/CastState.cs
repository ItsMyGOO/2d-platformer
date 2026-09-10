using Godot;

namespace GodotGameTemplate.Gameplay.Characters.States;

/// <summary>
/// 剑气状态（镜像 AttackState）：前摇→弹体出膛（CastFired）→后摇；
/// 地面施法定身，空中保持惯性；互斥与打断同 Attack——期间不路由
/// 冲刺/攻击/剑气意图（结构保证），可被受击打断。
/// </summary>
public class CastState : CharacterState
{
    private float _elapsed;
    private bool _fired;

    public CastState(CharacterMotor motor)
        : base(motor) { }

    public override void Enter()
    {
        _elapsed = 0f;
        _fired = false;
        if (Motor.IsOnFloor)
        {
            Motor.Velocity = new Vector2(0f, Motor.Velocity.Y); // 地面施法定身
        }
    }

    public override void Process(in InputIntent intent, float delta, float gravity)
    {
        var cast = Motor.Config.Cast;
        _elapsed += delta;

        if (!_fired && _elapsed >= cast.WindupTime)
        {
            _fired = true;
            Motor.NotifyCastFired(); // 前摇结束瞬间出膛
        }

        if (Motor.IsOnFloor)
        {
            float vx = MoveTowards(Motor.Velocity.X, 0f, Motor.Config.Friction * delta);
            Motor.Velocity = new Vector2(vx, Motor.Velocity.Y);
        }
        else
        {
            ApplyGravity(delta, gravity); // 空中施法保持惯性
        }

        if (_elapsed >= cast.WindupTime + cast.RecoveryTime)
        {
            Motor.ReturnToLocomotion();
        }
    }

    /// <summary>剑气复用攻击动画（正式美术期再拆独立动画）。</summary>
    public override CharacterVisualState VisualState => CharacterVisualState.Attack;
}
