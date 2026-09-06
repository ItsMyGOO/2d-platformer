using System;
using Godot;

namespace GodotGameTemplate.Gameplay.Characters.Combat;

/// <summary>
/// 常驻接触伤害：每物理帧轮询重叠的 Hurtbox 并尝试结算——
/// Health 无敌帧天然限频（每次受击后无敌期内重复接触无效）。
/// 与挥击 Hitbox 互不影响；数值由编排者从配置下发。
/// </summary>
public partial class ContactDamager : Area2D
{
    private int _damage;
    private float _knockbackHorizontal;
    private float _knockbackVertical;

    /// <summary>由编排者下发数值（来源与挥击相同的 AttackConfig）。</summary>
    public void Configure(int damage, float knockbackHorizontal, float knockbackVertical)
    {
        _damage = damage;
        _knockbackHorizontal = knockbackHorizontal;
        _knockbackVertical = knockbackVertical;
    }

    public override void _PhysicsProcess(double delta)
    {
        foreach (Area2D area in GetOverlappingAreas())
        {
            if (area is Hurtbox hurtbox)
            {
                float direction = MathF.Sign(hurtbox.GlobalPosition.X - GlobalPosition.X);
                if (direction == 0f)
                {
                    direction = 1f;
                }
                hurtbox.ReceiveHit(
                    DamageInfo.Create(_damage, _knockbackHorizontal, _knockbackVertical, direction)
                ); // 拒伤（无敌/尸体）由 Health 静默吞掉，接触伤害无命中确认事件
            }
        }
    }
}
