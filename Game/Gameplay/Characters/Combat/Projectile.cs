using System;
using Godot;

namespace GodotGameTemplate.Gameplay.Characters.Combat;

/// <summary>
/// 直线弹体：沿发射方向匀速飞行，命中受击盒结算伤害、撞地形即销毁，
/// 超时自毁。由 ProjectileEmitter 发射；伤害/击退与发射者攻击配置同源。
/// </summary>
public partial class Projectile : Area2D
{
    [Export]
    public float Speed { get; set; } = 320f;

    [Export]
    public float MaxLifetime { get; set; } = 3f;

    private int _damage;
    private float _knockbackHorizontal;
    private float _knockbackVertical;
    private Vector2 _direction = Vector2.Right;
    private float _lifetime;

    /// <summary>
    /// 发射：设置数值与方向。targetHurtboxLayer 为目标受击盒的层位值（4=玩家/8=敌人），
    /// 碰撞掩码自动包含地形层（撞墙销毁）与目标层。
    /// </summary>
    public void Launch(
        Vector2 direction,
        int damage,
        float knockbackHorizontal,
        float knockbackVertical,
        uint targetHurtboxLayer
    )
    {
        _direction = direction.Normalized();
        _damage = damage;
        _knockbackHorizontal = knockbackHorizontal;
        _knockbackVertical = knockbackVertical;
        CollisionLayer = 0;
        CollisionMask = 1u | targetHurtboxLayer; // 地形 + 目标受击盒
        Rotation = _direction.Angle();
    }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        AreaEntered += OnAreaEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        GlobalPosition += _direction * Speed * (float)delta;
        _lifetime += (float)delta;
        if (_lifetime >= MaxLifetime)
        {
            QueueFree();
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        // 撞地形即销毁（mask 只含地形与目标受击盒）
        QueueFree();
    }

    private void OnAreaEntered(Area2D area)
    {
        if (area is Hurtbox hurtbox)
        {
            float direction = MathF.Sign(hurtbox.GlobalPosition.X - GlobalPosition.X);
            if (direction == 0f)
            {
                direction = _direction.X >= 0f ? 1f : -1f;
            }
            hurtbox.ReceiveHit(
                DamageInfo.Create(_damage, _knockbackHorizontal, _knockbackVertical, direction)
            );
            QueueFree();
        }
    }
}
