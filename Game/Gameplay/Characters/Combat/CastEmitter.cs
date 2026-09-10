using Godot;

namespace GodotGameTemplate.Gameplay.Characters.Combat;

/// <summary>
/// 剑气发射器：挂在角色上，订阅所属角色 Motor 的 CastFired（剑气前摇结束瞬间），
/// 朝面朝方向发射新月弹。发射逻辑照 ProjectileEmitter 模式；弹速由弹体场景
/// Speed 决定，伤害/击退沿配置 Cast 与挥击同源。
/// </summary>
public partial class CastEmitter : Node
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
        _motor.CastFired += OnCastFired;
    }

    public override void _ExitTree()
    {
        if (_motor != null)
        {
            _motor.CastFired -= OnCastFired;
        }
    }

    private void OnCastFired()
    {
        if (_character == null || _projectileScene == null || _character.Health.IsDead)
        {
            return;
        }

        var projectile = _projectileScene.Instantiate<Projectile>();
        Vector2 facing = new(_motor.Facing, 0f);
        projectile.GlobalPosition =
            _character.GlobalPosition
            + new Vector2(_muzzleOffset.X * _motor.Facing, _muzzleOffset.Y);
        // 弹体打到敌方受击盒：玩家发射 → 敌人受击盒（层4=值8）；敌人发射 → 玩家受击盒（层3=值4）
        uint targetLayer = _character.IsPlayer ? 8u : 4u;
        var cast = _motor.Config.Cast;
        projectile.Launch(
            facing,
            cast?.Damage ?? 1,
            cast?.KnockbackHorizontal ?? 200f,
            cast?.KnockbackVertical ?? 80f,
            targetLayer
        );
        GetTree().CurrentScene.AddChild(projectile);
    }
}
