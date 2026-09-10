using System;
using Godot;

namespace GodotGameTemplate.Gameplay.Characters.Combat;

/// <summary>
/// 弹体：直线（沿发射方向匀速）或弧线（初速度 + 可选重力抛物线）飞行，
/// 命中受击盒结算伤害、撞地形即销毁，超时自毁。由 ProjectileEmitter /
/// BombEmitter 发射；伤害/击退与发射者攻击配置同源。
/// </summary>
public partial class Projectile : Area2D
{
    [Export]
    public float Speed { get; set; } = 320f;

    [Export]
    public float MaxLifetime { get; set; } = 3f;

    [Export]
    public float Gravity { get; set; } = 0f; // 0=直线（现状）；>0=抛物线

    private int _damage;
    private float _knockbackHorizontal;
    private float _knockbackVertical;
    private Vector2 _direction = Vector2.Right;
    private Vector2 _velocity = Vector2.Right; // 弧线模式的速度向量
    private bool _isArc;
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
        _isArc = false;
        _direction = direction.Normalized();
        Setup(damage, knockbackHorizontal, knockbackVertical, targetHurtboxLayer);
        Rotation = _direction.Angle();
    }

    /// <summary>弧线发射：初速度向量 + 可选重力（弹道由抛物线决定）。目标层语义同 Launch。</summary>
    public void LaunchArc(
        Vector2 initialVelocity,
        int damage,
        float knockbackHorizontal,
        float knockbackVertical,
        uint targetHurtboxLayer
    )
    {
        _isArc = true;
        _velocity = initialVelocity;
        Setup(damage, knockbackHorizontal, knockbackVertical, targetHurtboxLayer);
        Rotation = _velocity.Angle();
    }

    private void Setup(
        int damage,
        float knockbackHorizontal,
        float knockbackVertical,
        uint targetHurtboxLayer
    )
    {
        _damage = damage;
        _knockbackHorizontal = knockbackHorizontal;
        _knockbackVertical = knockbackVertical;
        CollisionLayer = 32u; // 第 6 层 projectile（可被近战劈碎）
        CollisionMask = 1u | targetHurtboxLayer; // 地形 + 目标受击盒
    }

    /// <summary>被近战劈碎（Pogo 借力）：立即销毁。</summary>
    public void Struck() => QueueFree();

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        AreaEntered += OnAreaEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_isArc)
        {
            _velocity.Y += Gravity * dt;
            GlobalPosition += _velocity * dt;
            Rotation = _velocity.Angle(); // 贴图随弹道旋转
        }
        else
        {
            GlobalPosition += _direction * Speed * dt;
        }

        _lifetime += dt;
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
                direction = _isArc
                    ? (_velocity.X >= 0f ? 1f : -1f)
                    : (_direction.X >= 0f ? 1f : -1f);
            }
            hurtbox.ReceiveHit(
                DamageInfo.Create(_damage, _knockbackHorizontal, _knockbackVertical, direction)
            );
            QueueFree();
        }
    }
}
