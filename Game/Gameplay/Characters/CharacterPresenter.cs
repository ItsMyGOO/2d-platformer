using System;
using Godot;
using GodotGameTemplate.Gameplay.Characters.Combat;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 表现层：把逻辑层状态翻译为动画、朝向与粒子特效。
/// 只读逻辑层，绝不反向写入；不包含任何玩法规则。
/// 精灵/粒子/朝向轴默认按类型或约定名发现，也可通过导出属性显式指定。
/// </summary>
public partial class CharacterPresenter : Node
{
    [Export]
    private AnimatedSprite2D _sprite;

    [Export]
    private CpuParticles2D _dustParticles;

    /// <summary>朝向轴：翻转它让精灵与攻击判定框一起换边。</summary>
    [Export]
    private Node2D _pivot;

    private CharacterMotor _motor;
    private Health _health;

    /// <summary>由编排者在逻辑层创建后调用，绑定引用并订阅运动事件。</summary>
    public void Bind(CharacterMotor motor, Health health)
    {
        _sprite ??= GetParent().FindDescendant<AnimatedSprite2D>();
        _dustParticles ??= GetParent().FindDescendant<CpuParticles2D>();
        _pivot ??= GetParent().GetNodeOrNull<Node2D>("Pivot");
        Unbind();
        _motor = motor;
        _health = health;
        _motor.Jumped += OnJumped;
        _motor.Landed += OnLanded;
    }

    private void Unbind()
    {
        if (_motor == null)
        {
            return;
        }
        _motor.Jumped -= OnJumped;
        _motor.Landed -= OnLanded;
        _motor = null;
        _health = null;
    }

    public override void _ExitTree() => Unbind();

    /// <summary>由编排者每物理帧调用一次（保证在逻辑层更新之后执行）。</summary>
    public void Sync()
    {
        if (_motor == null || _sprite == null)
        {
            return;
        }
        _sprite.Play(_motor.VisualState.ToString().ToLowerInvariant());

        if (_pivot != null)
        {
            float absScaleX = MathF.Abs(_pivot.Scale.X);
            _pivot.Scale = new Vector2(_motor.Facing < 0 ? -absScaleX : absScaleX, _pivot.Scale.Y);
        }
        else
        {
            _sprite.FlipH = _motor.Facing < 0;
        }

        SyncInvincibilityBlink();
    }

    /// <summary>受击无敌帧期间精灵闪烁；死亡时保持可见。</summary>
    private void SyncInvincibilityBlink()
    {
        if (_health == null || _motor.VisualState == CharacterVisualState.Dead)
        {
            _sprite.Modulate = Colors.White;
            return;
        }
        bool blinkOff = _health.IsInvincible && Time.GetTicksMsec() / 80 % 2 == 0;
        _sprite.Modulate = blinkOff ? new Color(1f, 1f, 1f, 0.35f) : Colors.White;
    }

    private void OnJumped()
    {
        if (_dustParticles != null)
        {
            _dustParticles.Restart();
        }
    }

    private void OnLanded()
    {
        if (_dustParticles != null)
        {
            _dustParticles.Restart();
        }
    }
}
