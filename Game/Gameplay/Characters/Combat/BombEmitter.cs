using Godot;

namespace GodotGameTemplate.Gameplay.Characters.Combat;

/// <summary>
/// 炸弹发射器：挂角色上，订阅所属角色 Motor 的 AttackActiveChanged(true)
/// （攻击窗口开启瞬间出弹）。解算抛物线初速使炸弹落到玩家所在位置
/// （y 轴向下为正）：vx = dx / T；vy = (dy − ½·G·T²) / T。
/// 找不到玩家（组 "player"）→ 朝面朝方向平射兜底。AI 复用 ShooterAI 输入源。
/// </summary>
public partial class BombEmitter : Node
{
    [Export]
    private PackedScene _projectileScene;

    /// <summary>出弹偏移（像素，沿面朝方向）。</summary>
    [Export]
    private Vector2 _muzzleOffset = new(16f, -10f);

    /// <summary>炸弹重力（像素/秒²，与 Bomb 场景一致；用于落点解算）。</summary>
    [Export]
    public float Gravity { get; set; } = 900f;

    /// <summary>飞行时间（秒），落点由抛物线方程确定。</summary>
    [Export]
    public float FlightTime { get; set; } = 0.9f;

    /// <summary>射程上限（像素），超过按上限落点投掷。</summary>
    [Export]
    public float MaxRange { get; set; } = 420f;

    private Character _character;
    private CharacterMotor _motor;

    /// <summary>由编排者在装配时调用。</summary>
    public void Bind(Character character)
    {
        _character = character;
        _motor = character.Motor;
        _motor.AttackActiveChanged += OnAttackActiveChanged;
    }

    public override void _ExitTree()
    {
        if (_motor != null)
        {
            _motor.AttackActiveChanged -= OnAttackActiveChanged;
        }
    }

    private void OnAttackActiveChanged(bool active)
    {
        if (!active || _character == null || _projectileScene == null || _character.Health.IsDead)
        {
            return;
        }

        var projectile = _projectileScene.Instantiate<Projectile>();
        projectile.GlobalPosition =
            _character.GlobalPosition
            + new Vector2(_muzzleOffset.X * _motor.Facing, _muzzleOffset.Y);
        // 弹体打到对方受击盒：敌人发射 → 玩家受击盒（层3=值4）
        uint targetLayer = _character.IsPlayer ? 8u : 4u;
        var attack = _motor.Config.Attack;

        var player = GetTree().GetFirstNodeInGroup("player") as Character;
        Vector2 muzzle = projectile.GlobalPosition;
        Vector2 velocity;
        if (player != null)
        {
            float dx = Mathf.Clamp(player.GlobalPosition.X - muzzle.X, -MaxRange, MaxRange);
            float dy = player.GlobalPosition.Y - muzzle.Y;
            float vx = dx / FlightTime;
            float vy = (dy - 0.5f * Gravity * FlightTime * FlightTime) / FlightTime;
            velocity = new Vector2(vx, vy);
        }
        else
        {
            velocity = new Vector2(_motor.Facing * 150f, -150f); // 平射前方兜底
        }

        projectile.LaunchArc(
            velocity,
            attack?.Damage ?? 1,
            attack?.KnockbackHorizontal ?? 160f,
            attack?.KnockbackVertical ?? 120f,
            targetLayer
        );
        GetTree().CurrentScene.AddChild(projectile);
    }
}
