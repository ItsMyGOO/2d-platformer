using System;
using System.Collections.Generic;
using Godot;

namespace GodotGameTemplate.Gameplay.Characters.Combat;

/// <summary>
/// 攻击判定框：攻击激活窗口内每物理帧轮询重叠区域，命中 Hurtbox 结算伤害。
/// 同一次挥击对同一目标只结算一次。伤害与击退数值由编排者从角色配置下发（唯一事实源）。
/// </summary>
public partial class Hitbox : Area2D
{
    private int _damage;
    private float _knockbackHorizontal;
    private float _knockbackVertical;

    private readonly HashSet<Area2D> _hitThisSwing = new(); // 受击盒与被劈弹体共用（同挥击各一次）
    private ColorRect _debugVisual;
    private CollisionShape2D _hitShape;

    private static readonly Vector2 SideHitPose = new(22f, 0f);
    private static readonly Vector2 DownHitPose = new(0f, 26f);

    /// <summary>命中确认：每次真实结算成功（非无敌/非尸体拒伤）后触发，表现层用作命中反馈。</summary>
    public event Action HitConfirmed;

    /// <summary>由编排者下发攻击数值（来源 AttackConfig）。</summary>
    public void Configure(int damage, float knockbackHorizontal, float knockbackVertical)
    {
        _damage = damage;
        _knockbackHorizontal = knockbackHorizontal;
        _knockbackVertical = knockbackVertical;
    }

    public override void _Ready()
    {
        Monitoring = false;
        Monitorable = false; // 判定框永远只打人不被人打
        _debugVisual = GetNodeOrNull<ColorRect>("DebugVisual");
        _hitShape = GetNode<CollisionShape2D>("HitShape");
    }

    /// <summary>切换命中盒朝向：常规侧向 (22,0) ↔ 空中下劈 (0,26)（由 AttackStarted 驱动）。</summary>
    public void SetDownOrientation(bool down)
    {
        _hitShape.Position = down ? DownHitPose : SideHitPose;
    }

    /// <summary>开启新一轮挥击：清空已命中集合（由 Motor.AttackStarted 驱动）。</summary>
    public void BeginSwing() => _hitThisSwing.Clear();

    /// <summary>开关判定激活窗口（由 Motor.AttackActiveChanged 驱动）。</summary>
    public void SetActive(bool active)
    {
        Monitoring = active;
        if (active)
        {
            _hitThisSwing.Clear();
        }
        if (_debugVisual != null)
        {
            _debugVisual.Visible = active;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Monitoring)
        {
            return;
        }
        foreach (Area2D area in GetOverlappingAreas())
        {
            if (area is Hurtbox hurtbox && _hitThisSwing.Add(hurtbox))
            {
                float direction = MathF.Sign(hurtbox.GlobalPosition.X - GlobalPosition.X);
                if (direction == 0f)
                {
                    direction = 1f;
                }
                if (
                    hurtbox.ReceiveHit(
                        DamageInfo.Create(
                            _damage,
                            _knockbackHorizontal,
                            _knockbackVertical,
                            direction
                        )
                    )
                )
                {
                    HitConfirmed?.Invoke(); // 只在真实结算（拒伤不计）时确认命中
                }
            }
            else if (area is Projectile projectile)
            {
                // 劈碎弹体（下劈 Pogo 借力含弹体）；同挥击只劈一次
                if (_hitThisSwing.Add(projectile))
                {
                    projectile.Struck();
                    HitConfirmed?.Invoke();
                }
            }
        }
    }
}
