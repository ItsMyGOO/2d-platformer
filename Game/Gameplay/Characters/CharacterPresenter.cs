using Godot;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 表现层：把逻辑层状态翻译为动画、朝向与粒子特效。
/// 只读逻辑层，绝不反向写入；不包含任何玩法规则。
/// 精灵与粒子默认在角色根的直接子节点中按类型发现，
/// 也可通过导出属性显式指定。
/// </summary>
public partial class CharacterPresenter : Node
{
    [Export]
    private AnimatedSprite2D _sprite;

    [Export]
    private CpuParticles2D _dustParticles;

    private CharacterMotor _motor;

    /// <summary>由编排者在逻辑层创建后调用，绑定视觉引用并订阅运动事件。</summary>
    public void Bind(CharacterMotor motor)
    {
        _sprite ??= FindSibling<AnimatedSprite2D>();
        _dustParticles ??= FindSibling<CpuParticles2D>();
        Unbind();
        _motor = motor;
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
    }

    public override void _ExitTree() => Unbind();

    /// <summary>由编排者每物理帧调用一次（保证在逻辑层更新之后执行）。</summary>
    public void Sync()
    {
        if (_motor == null || _sprite == null)
        {
            return;
        }
        _sprite.Play(_motor.State.ToString().ToLowerInvariant());
        _sprite.FlipH = _motor.Facing < 0;
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

    /// <summary>在角色根的直接子节点中查找第一个指定类型节点（排除自身）。</summary>
    private T FindSibling<T>()
        where T : Node
    {
        foreach (Node child in GetParent().GetChildren())
        {
            if (child != this && child is T typed)
            {
                return typed;
            }
        }
        return null;
    }
}
