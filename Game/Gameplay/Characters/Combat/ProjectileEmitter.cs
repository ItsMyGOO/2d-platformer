using Godot;

namespace GodotGameTemplate.Gameplay.Characters.Combat;

/// <summary>
/// 弹体发射器：挂在角色上，订阅所属角色 Motor 的 AttackStarted，
/// 在攻击判定窗口开启（AttackActiveChanged=true，即前摇结束）的瞬间朝面朝方向发射弹体。
/// 弹体场景与数值由导出/配置提供；沿祖先链取攻击数值（与挥击/接触伤害同源 AttackConfig）。
/// </summary>
public partial class ProjectileEmitter : Node
{
    [Export]
    private PackedScene _projectileScene;

    /// <summary>弹体出膛偏移（像素，沿面朝方向）。</summary>
    [Export]
    private Vector2 _muzzleOffset = new(22f, -2f);

    private Character _character;
    private CharacterMotor _motor;

    /// <summary>由编排者在装配时调用。</summary>
    public void Bind(Character character)
    {
        _character = character;
        _motor = character.Motor;
        _motor.AttackStarted += OnAttackStarted;
        _motor.AttackActiveChanged += OnAttackActiveChanged;
    }

    public override void _ExitTree()
    {
        if (_motor != null)
        {
            _motor.AttackStarted -= OnAttackStarted;
            _motor.AttackActiveChanged -= OnAttackActiveChanged;
        }
    }

    private void OnAttackStarted() { }

    private void OnAttackActiveChanged(bool active)
    {
        if (!active || _character == null || _projectileScene == null || _character.Health.IsDead)
        {
            return;
        }
        Emit();
    }

    private void Emit()
    {
        var projectile = _projectileScene.Instantiate<Projectile>();
        Vector2 facing = new(_motor.Facing, 0f);
        projectile.GlobalPosition =
            _character.GlobalPosition
            + new Vector2(_muzzleOffset.X * _motor.Facing, _muzzleOffset.Y);
        // 弹体打到敌方受击盒：玩家发射 → 敌人受击盒（层4=值8）；敌人发射 → 玩家受击盒（层3=值4）
        uint targetLayer = _character.IsPlayer ? 8u : 4u;
        var attack = _motor.Config.Attack;
        projectile.Launch(
            facing,
            attack?.Damage ?? 1,
            attack?.KnockbackHorizontal ?? 160f,
            attack?.KnockbackVertical ?? 120f,
            targetLayer
        );
        GetTree().CurrentScene.AddChild(projectile);
    }
}
