using Godot;

namespace GodotGameTemplate.Gameplay.Characters.States;

/// <summary>
/// 攻击状态：前摇→判定激活→后摇三段窗口；期间忽略冲刺与跳跃意图。
/// 地面攻击定身，空中攻击保持惯性；可被受击打断（Exit 时确保判定关闭）。
/// </summary>
public class AttackState : CharacterState
{
    private float _elapsed;
    private bool _activeOpened;
    private bool _activeClosed;

    public AttackState(CharacterMotor motor)
        : base(motor) { }

    public override void Enter()
    {
        _elapsed = 0f;
        _activeOpened = false;
        _activeClosed = false;
        if (Motor.IsOnFloor)
        {
            Motor.Velocity = new Vector2(0f, Motor.Velocity.Y); // 地面攻击定身
        }
    }

    public override void Exit()
    {
        if (_activeOpened && !_activeClosed)
        {
            Motor.NotifyAttackActive(false); // 被打断时确保判定关闭
        }
    }

    public override void Process(in InputIntent intent, float delta, float gravity)
    {
        AttackConfig attack = Motor.Config.Attack;
        _elapsed += delta;

        if (!_activeOpened && _elapsed >= attack.WindupTime)
        {
            _activeOpened = true;
            Motor.NotifyAttackActive(true);
        }
        if (!_activeClosed && _elapsed >= attack.WindupTime + attack.ActiveTime)
        {
            _activeClosed = true;
            Motor.NotifyAttackActive(false);
        }

        if (Motor.IsOnFloor)
        {
            float vx = MoveTowards(Motor.Velocity.X, 0f, Motor.Config.Friction * delta);
            Motor.Velocity = new Vector2(vx, Motor.Velocity.Y);
        }
        else
        {
            ApplyGravity(delta, gravity); // 空中攻击保持惯性
        }

        if (_elapsed >= attack.WindupTime + attack.ActiveTime + attack.RecoveryTime)
        {
            Motor.ReturnToLocomotion();
        }
    }

    public override CharacterVisualState VisualState => CharacterVisualState.Attack;
}
