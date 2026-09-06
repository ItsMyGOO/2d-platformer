using System;
using System.Collections.Generic;
using Godot;

namespace GodotGameTemplate.Gameplay.Characters.Combat;

/// <summary>
/// 攻击判定框：攻击激活窗口内每物理帧轮询重叠区域，命中 Hurtbox 结算伤害。
/// 同一次挥击对同一目标只结算一次。判定数值在场景中按角色配置。
/// </summary>
public partial class Hitbox : Area2D
{
    [Export]
    private int _damage = 1;

    [Export]
    private float _knockbackHorizontal = 160f;

    [Export]
    private float _knockbackVertical = 120f;

    private readonly HashSet<Hurtbox> _hitThisSwing = new();
    private ColorRect _debugVisual;

    /// <summary>命中确认：每次实际结算伤害后触发一次（表现层用作命中反馈）。</summary>
    public event Action HitConfirmed;

    public override void _Ready()
    {
        Monitoring = false;
        Monitorable = false; // 判定框永远只打人不被人打
        _debugVisual = GetNodeOrNull<ColorRect>("DebugVisual");
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
                hurtbox.ReceiveHit(
                    DamageInfo.Create(_damage, _knockbackHorizontal, _knockbackVertical, direction)
                );
                HitConfirmed?.Invoke();
            }
        }
    }
}
